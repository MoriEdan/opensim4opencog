using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using OpenMetaverse;
using Radegast;

namespace CogbotRadegastPluginModule
{
    public class AspectContextAction : ContextAction
    {
        public Object lastObject;
        CogbotTabWindow console
        {
            get { return (CogbotTabWindow)instance.TabConsole.GetTab("cogbot").Control; }
        }
        private ContextMenuStrip ExtraContextMenu
        {
            get { return console.PluginExtraContextMenu; }
        }
        public AspectContextAction(RadegastInstance radegastInstance)
            : base(radegastInstance)
        {
            ContextType = typeof (Object);
            Label = "cogbot...";
            Client.Network.OnLogin += aspectLogin;
        }

        public Dictionary<string, List<ToolStripMenuItem>> MenuItems = new Dictionary<string, List<ToolStripMenuItem>>();
        private void aspectLogin(LoginStatus login, string message)
        {
            if (login!=LoginStatus.Success) return;
            ScanCogbotMenu();
        }

        private void ScanCogbotMenu()
        {
            foreach (var c in ExtraContextMenu.Items)
            {
                ToolStripMenuItem t = (ToolStripMenuItem) c;
                List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
                if (!t.HasDropDownItems) continue;
                foreach (ToolStripMenuItem item in t.DropDownItems)
                {
                    HookItem(item);
                    lst.Add(item);
                }
                MenuItems[t.Text] = lst;
            }
        }

        private void HookItem(ToolStripDropDownItem t)
        {
            t.Click += SubHook;
            t.Tag = this;
            if (!t.HasDropDownItems) return;
            foreach (ToolStripMenuItem item in t.DropDownItems)
            {
                HookItem(item);
            }
        }

        private void SubHook(object sender, EventArgs e)
        {
            TryCatch(() =>
                         {
                             if (sender != lastObject && sender is ToolStripItem)
                                 FakeEvent(sender, "Click", lastObject, e);
                         });
            instance.TabConsole.DisplayNotificationInChat(
                string.Format("SubHook sender={0}\nlastObect={1}", ToString(sender),
                              ToString(lastObject)));
        }

        public void FakeEvent(Object target, String infoName, params object[] parameters)
        {
            Type type = target.GetType();
            EventInfo eventInfo = type.GetEvent(infoName);
            MethodInfo m = eventInfo.GetRaiseMethod();

            Exception lastException = null;
            if (m != null)
            {
                try
                {


                    m.Invoke(target, parameters);
                    return;
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            }
            else
            {
                {
                    FieldInfo fieldInfo = type.GetField(eventInfo.Name,
                                                        BindingFlags.Instance | BindingFlags.NonPublic |
                                                        BindingFlags.Public);
                    if (fieldInfo != null)
                    {
                        Delegate del = fieldInfo.GetValue(target) as Delegate;

                        if (del != null)
                        {
                            del.DynamicInvoke(parameters);
                            return;
                        }
                    }
                }
            }
            if (lastException != null) throw lastException;
            throw new NotSupportedException();
        }

        public override bool Contributes(object o, Type type)
        {
            return true;
        }
        public override bool IsEnabled(object target)
        {
            return true;
        }
        public override IEnumerable<ToolStripMenuItem> GetToolItems(object target, Type type)
        {
            List<ToolStripMenuItem> lst = new List<ToolStripMenuItem>();
            HashSet<Type> types = new HashSet<Type>();
            AddTypes(type, types);
            lastObject = target;
            if (target!=null)
            {
                target = DeRef(lastObject);

                AddTypes(lastObject.GetType(), types);
                if (target != null && target != lastObject)
                {
                    AddTypes(target.GetType(), types);
                }
            }
            foreach (Type e in types)
            {
                IEnumerable<ToolStripMenuItem> v = GetToolItemsType(e);
                if (v!=null)lst.AddRange(GetToolItemsType(e));
            }
            UUID uuid = ToUUID(target);
            if (uuid != UUID.Zero)
            {
                lst.Add(new ToolStripMenuItem("Copy UUID", null,
                                                (sender, e) =>
                                                {
                                                    DebugLog("UUID=" + uuid);
                                                })
                {
                    ToolTipText = "UUID=" + uuid
                });
 
            }
            return lst;
             
        }

        private bool AddTypes(Type type, HashSet<Type> types)
        {
            if (type == null || type == typeof(Object)|| !types.Add(type)) return false;
            bool changed = AddTypes(type.BaseType, types);
            foreach (Type t in type.GetInterfaces())
            {
                if (AddTypes(t, types)) changed = true;
            }
            return changed;
        }

        private IEnumerable<ToolStripMenuItem> GetToolItemsType(Type typ)
        {
            String type = typ.Name;
            //target = DeRef(target);
            List<ToolStripMenuItem> found = null;
            foreach (var c in MenuItems)
            {
                if (type.EndsWith(c.Key))
                {
                    found = c.Value;
                    break;
                }                
            }
            if (found == null)
            {
                //return base.GetToolItems(target);
                return null;
            }
            //foreach (ToolStripMenuItem item in found)
            //{
            //    AddCallback(target, item);
            //    //item.Closing += ((sender, args) => items.ForEach((o) => strip.Items.Remove(o)));
            //}
            return found;
        }

        //private void AddCallback(object target, ToolStripMenuItem item)
        //{
        //    if (item.HasDropDownItems)
        //    {
        //        foreach (var pair in item.DropDownItems)
        //        {
        //            AddCallback(target, item); 
        //        }
        //    }
        //    EventHandler act = (sender, e) =>
        //           instance.TabConsole.DisplayNotificationInChat(
        //               string.Format(" sender={0}\ntarget={1}", ToString(sender), ToString(target)));
        //    item.Click += act;

        //    EventHandler reg = (sender, e) =>
        //                             {
                                         
        //                             };
        //    EventHandler ureg = (sender, e) =>
        //    {

        //    };
        //    //item.OwnerChanged += reg;

                
        //    EventHandler dereg = (sender, e) =>
        //                             {
        //                                 item.Click -= act;
        //                                 item.Click += act;
        //                             };

        //    item.OwnerChanged += dereg;
        //    item.LocationChanged += dereg;
        //}

        private string ToString(object sender)
        {
            string t = sender.GetType().Name + ":";
            if (sender is Control)
            {
                Control control = (Control)sender;
                return string.Format("{0}{1} {2} {3}", t, control.Text, control.Name, ToString(control.Tag));
            }
            if (sender is ListViewItem)
            {
                ListViewItem control = (ListViewItem)sender;
                return string.Format("{0}{1} {2} {3}", t, control.Text, control.Name, ToString(control.Tag));
            }
            return t + sender;
        }
        //public override string LabelFor(object target)
        //{
        //    return target.GetType().Name;
        //}
        public object GetValue(Type type)
        {
            if (type.IsInstanceOfType(lastObject)) return lastObject;
            if (type.IsAssignableFrom(typeof(Primitive))) return ToPrimitive(lastObject);
            if (type.IsAssignableFrom(typeof(Avatar))) return ToAvatar(lastObject);
            if (type.IsAssignableFrom(typeof(UUID))) return ToUUID(lastObject);
            return lastObject;
        }
    }
}