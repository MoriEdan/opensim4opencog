﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace HttpServer
{
    /// <summary>
    /// Contains a listener that doesn't do anything with the connections.
    /// </summary>
    public abstract class HttpListenerBase
    {
        #region Events

        /// <summary>
        /// Catch exceptions not handled by the listener.
        /// </summary>
        /// <remarks>
        /// Exceptions will be thrown during debug mode if this event is not used,
        /// exceptions will be printed to console and suppressed during release mode.
        /// </remarks>
        public event ExceptionHandler ExceptionThrown;

        /// <summary>
        /// A request have been received from a <see cref="IHttpClientContext"/>.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate { };

        #endregion Events

        /// <summary>
        /// Can be used to create filtering of new connections.
        /// </summary>
        /// <param name="socket">Accepted socket</param>
        /// <returns>true if connection can be accepted; otherwise false.</returns>
        protected abstract bool OnAcceptingSocket(Socket socket);

        protected ILogWriter _logWriter = NullLogWriter.Instance;

        private readonly IPAddress _address;
        private readonly X509Certificate _certificate;
        private readonly IHttpContextFactory _factory;
        private readonly int _port;
        private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private readonly SslProtocols _sslProtocol = SslProtocols.Default;
        private readonly bool _requireClientCerts;
        private TcpListener _listener;
        private int _pendingAccepts;
        private bool _shutdown;

        #region Properties

        /// <summary>
        /// True if we should turn on trace logs.
        /// </summary>
        public bool UseTraceLogs { get; set; }

        /// <summary>
        /// Gives you a change to receive log entries for all internals of the HTTP library.
        /// </summary>
        /// <remarks>
        /// You may not switch log writer after starting the listener.
        /// </remarks>
        public ILogWriter LogWriter
        {
            get { return _logWriter; }
            set
            {
                _logWriter = value ?? NullLogWriter.Instance;
                if (_certificate != null)
                    _logWriter.Write(this, LogPrio.Info,
                                     "HTTPS(" + _sslProtocol + ") listening on " + _address + ":" + _port);
                else
                    _logWriter.Write(this, LogPrio.Info, "HTTP listening on " + _address + ":" + _port);
            }
        }

        #endregion Properties

        /// <summary>
        /// Listen for regular HTTP connections
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTP port is 80.</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <exception cref="ArgumentNullException"><c>address</c> is null.</exception>
        /// <exception cref="ArgumentException">Port must be a positive number.</exception>
        protected HttpListenerBase(IPAddress address, int port, IHttpContextFactory factory)
        {
            Check.Require(address, "address");
            Check.Min(1, port, "port");
            Check.Require(factory, "factory");

            _address = address;
            _port = port;
            _factory = factory;
            _factory.RequestReceived += OnRequestReceived;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpListenerBase"/> class.
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTPS port is 443</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <param name="certificate">Certificate to use</param>
        /// <param name="protocol">which HTTPS protocol to use, default is TLS.</param>
        /// <param name="requireClientCerts">True if client SSL certificates are required, otherwise false</param>
        protected HttpListenerBase(IPAddress address, int port, IHttpContextFactory factory, X509Certificate certificate,
                                   SslProtocols protocol, bool requireClientCerts)
            : this(address, port, factory)
        {
            Check.Require(certificate, "certificate");

            _certificate = certificate;
            _requireClientCerts = requireClientCerts;
            _sslProtocol = protocol;
        }

        /// <summary>
        /// Start listen for new connections
        /// </summary>
        /// <param name="backlog">Number of connections that can stand in a queue to be accepted.</param>
        /// <exception cref="InvalidOperationException">Listener have already been started.</exception>
        public void Start(int backlog)
        {
            if (_listener != null)
                throw new InvalidOperationException("Listener have already been started.");

            _listener = new TcpListener(_address, _port);
            _listener.Start(backlog);
            Interlocked.Increment(ref _pendingAccepts);
            _listener.BeginAcceptSocket(OnAccept, null);
        }

        /// <summary>
        /// Stop the listener
        /// </summary>
        /// <exception cref="SocketException"></exception>
        public virtual void Stop()
        {
            _shutdown = true;
            _listener.Stop();
            if (!_shutdownEvent.WaitOne())
                _logWriter.Write(this, LogPrio.Error, "Failed to shutdown listener properly.");
            _listener = null;
        }

        /// <exception cref="Exception"><c>Exception</c>.</exception>
        private void OnAccept(IAsyncResult ar)
        {
            bool beginAcceptCalled = false;
            try
            {
                int count = Interlocked.Decrement(ref _pendingAccepts);
                if (_shutdown)
                {
                    if (count == 0)
                        _shutdownEvent.Set();
                    return;
                }

                Interlocked.Increment(ref _pendingAccepts);
                _listener.BeginAcceptSocket(OnAccept, null);
                beginAcceptCalled = true;
                Socket socket = _listener.EndAcceptSocket(ar);

                if (!OnAcceptingSocket(socket))
                {
                    socket.Disconnect(true);
                    return;
                }

                _logWriter.Write(this, LogPrio.Debug, "Accepted connection from: " + socket.RemoteEndPoint);

                IHttpClientContext clientContext;

                if (_certificate != null)
                    clientContext = _factory.CreateSecureContext(socket, _certificate, _sslProtocol, _requireClientCerts);
                else
                    clientContext = _factory.CreateContext(socket);

                if (clientContext == null)
                    socket.Disconnect(true);
            }
            catch (Exception err)
            {
                ThrowException(err);

                if (!beginAcceptCalled)
                    RetryBeginAccept();
            }
        }

        /// <summary>
        /// Will try to accept connections one more time.
        /// </summary>
        /// <exception cref="Exception">If any exceptions is thrown.</exception>
        private void RetryBeginAccept()
        {
            try
            {
                _logWriter.Write(this, LogPrio.Error, "Trying to accept connections again.");
                _listener.BeginAcceptSocket(OnAccept, null);
            }
            catch (Exception err)
            {
                ThrowException(err);
            }
        }

        protected void ThrowException(Exception err)
        {
            ExceptionHandler handler = ExceptionThrown;

            if (handler != null)
            {
                handler(this, err);
            }
            else
            {
                _logWriter.Write(this, LogPrio.Fatal, err.Message);
            }
        }

        private void OnRequestReceived(object sender, RequestEventArgs e)
        {
            RequestReceived(sender, e);
        }
    }
}
