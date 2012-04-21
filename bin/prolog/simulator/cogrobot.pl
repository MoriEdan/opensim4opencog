%------------------------------------------------------------------------------
%
%  cogrobot.pl
%
%     Module for use of Cogbot in SecondLife!!!
%
% Cogbot is usually in this mode
% set_prolog_flag(double_quotes,string).
%
%------------------------------------------------------------------------------
:-module(cogrobot,
  [
   runSL/0,listMembs/0,
   worldSystem/1, worldSystem/2,
   botClient/1, botClient/2,
   botClientCall/1, botClientCall/2,
   botClientCmd/1, botClientCmd/2, botClientCmd/3,
   simObject/1, simAvatar/1, simAvDistance/3, simAsset/1, simAccount/1,
   gridClient/1,
   resolveObjectByName/2,
   vectorAdd/3,
   distanceTo/2,
   toGlobalVect/2,
   toLocalVect/2,
   onSimEvent/3,wasSimEvent/3,
   obj2Npl/2,
   npl2Obj/2,
   chat/1,
   chat/2,
   chat/3,
   %%cliWriteFormat/3,
   createWritelnDelegate/2,
   createWritelnDelegate/1,
   textureIDToImage/2,
   textureIDToImageParts/2,
   requestTexture/1,
   simObjectColor/2
   ]).

:-set_prolog_flag(double_quotes,string).

atInit(Call):-term_to_atom(Call,Atom),atom_concat(Atom,'_done',Did),dynamic(Did),atInitCall(atInit(Did,Call)).
atInitCall(Call):-at_initialization(Call),Call.
atInit(Did,_Call):-Did,!.
atInit(Did,Call):-assert(Did),!,Call.


%%:- absolute_file_name('.',X),asserta(prev_dir6(X)),listing(prev_dir6).

%%:-source_location(File,_Line),file_directory_name(File, Directory),cd(Directory).


assertIfNew(Gaf):-catch(call(Gaf),_,fail),!.
assertIfNew(Gaf):-assert(Gaf).

:- assertIfNew(user:file_search_path(foreign, '.')).
:- assertIfNew(user:file_search_path(jpl_examples, 'examples/prolog')).
:- assertIfNew(user:file_search_path(jar, '.')).
:- assertIfNew(user:file_search_path(library, '.')).
:- assertIfNew(user:file_search_path(library, '..')).
:- assertIfNew(user:file_search_path(library, '../..')).
:- assertIfNew(user:file_search_path(library, '../../test')).
:- assertIfNew(user:file_search_path(test, '../test')).

%%:- use_module(library(testsupport)).
:-use_module(library(swicli)).


%:-atInit(cliLoadAssembly('AForge.Imaging.dll')).
%:-atInit(cliLoadAssembly('AForge.Imaging.Formats.dll')).
%------------------------------------------------------------------------------

%% load needed modules

:-use_module(library(swicli)).
%%:-use_module(library(jpl)).

%------------------------------------------------------------------------------

%% load the cogbot assembly
:-dynamic(loadedCogbotAssembly/0).
loadCogbotAssembly:-loadedCogbotAssembly,!.
loadCogbotAssembly:-assert(loadedCogbotAssembly),current_prolog_flag(address_bits,32) -> cliLoadAssembly('Cogbot32.exe') ; cliLoadAssembly('Cogbot.exe').
:-atInit(loadCogbotAssembly).

%% cache the type names
% prevents us having to use long names for things like SimAvatar
%
cacheShortNames:-
  cliMembers('cogbot.TheOpenSims.SimAvatar',_),
  cliMembers('cogbot.Listeners.WorldObjects',_),
  cliMembers('OpenMetaverse.Primitive',_).

:-atInit(cacheShortNames).
%------------------------------------------------------------------------------
% some type layout conversions (to make cleaner code)
%
%  Layouts are records - it's a field layout
%  cliToFromLayout is a way to register an automagic conversion type
%  cliAddLayout adds a conversion between C# type and Prolog type
%------------------------------------------------------------------------------

addLayouts:-
  cliAddLayout('Vector3',v3(x,y,z)),
  cliAddLayout('Vector3d',v3d('X','Y','Z')),
  cliAddLayout('Vector4',v4('X','Y','Z','W')),
  cliAddLayout('Quaternion',quat('X','Y','Z','W')),
 %%  cliAddLayout('UUID',uuid('_Guid')),
 cliToFromLayout('UUID',uuid('ToString'),'UUIDFromString'),
 %%  cliAddLayout('Guid',guid(string)),
  !.

:-atInit(addLayouts).


%------------------------------------------------------------------------------
% object getter functions
%
% cliGet is a field accessor that says use the property, if you can't
% find that use the raw field and return.
%
% So this is 'get the GridMaster property from the static
% cogbot.Listeners.WorldObjects and return in Sys'
%
% ------------------------------------------------------------------------------
%
%

worldSystem(Sys):-cliGet('cogbot.Listeners.WorldObjects','GridMaster',Sys).

% gets some property of the GridMaster
%  worldSystem(+Field, -Value)
%
worldSystem(Field,Value):-worldSystem(Sys),cliGet(Sys,Field,Value).

%% get each SimObject
% this is every primitive, linked or not, as a complex term
% it's a partially marshalled object from the simulator
% cliCol iterates thru elements
simObject(Ele):-worldSystem('SimObjects',Objs),cliCol(Objs,Ele).

simRootObject(Ele):-worldSystem('SimRootObjects',Objs),cliCol(Objs,Ele).

% the above is simpler form of:   simObject(Ele):-worldSystem(Sys),cliGet(Sys,'SimObjects',Obj),cliCol(Obj,Ele).

%% get each SimAvatar
%
% this is the set of av's that are known to the simulator, they are only
% actually present if they have a prim
%
% simObject and simAvatar handle the complexity of sim crossings
%
simAvatar(Ele):-simAccount(Ele),cliGet(Ele,hasprim,@(true)).

%
% a simAccount/1 is like simAvatar (they are avatars known about in system..
%    including friends not logged in)
%
simAccount(Ele):-worldSystem('SimAvatars',Objs),cliCol(Objs,Ele).

%
% a simRegion predicate with simParcels (cli not done yet)
%
simRegion(Ele):-cliGet('cogbot.TheOpenSims.SimRegion','CurrentRegions',Objs),cliCol(Objs,Ele).

simParcel(Ele):-simRegion(Sim),cliGet(Sim,parcels,Objs),cliCol(Objs,Ele).

%% get the clientManager Instance
%%
%  A class that holds all the static singletons
%  A client is a logged on acct in this context
%
%  clientmanager binds radegast to the client
%  botconfig is run from clientmanager
clientManager(SingleInstance):-cliGet('cogbot.ClientManager','SingleInstance',SingleInstance).

%% get the botClient Instance
% this only unifies once, someday there will be a botClients
botClient(Obj):-clientManager(Man),cliGet(Man,'LastRefBotClient',Obj).

% given an object and a property returns value for the avatar
%
% walks down property tree
% botClient([name,length,X)
% botClient([name,length],X)
% botClient([position,z],X)
%
% a.b.c.d
% botClient([a.b.c.d],X).
% botClient([a,b,c,d],X).
% botClient([a,b,c,d],X). = mybot.a.b.c.d
% prolog botClient([a,b,c,d],X). = c# object X = mybotClient.a.b.c.d
% botClient(['Inventory','Store',rootfolder,name],Y).
% Y = "My Inventory".
%
% botClient(['Inventory','Store',rootnode,nodes,values],Y),
%	findall(S,(cliCol(Y,Z),cliToString(Z,S)),L),writeq(L).
%	["Scripts","Photo Album","*MD* Brown Leather Hat w/Bling",
%	"Body Parts","Notecards","Objects","Clothing","Landmarks","Textures",
%     "Gestures","boxed fem_talk","Calling Cards","Animations","Sounds",
%	"Trash","Lost And Found"]
%       Y = @'C#720558400'
%
%       finds all grandchildren
%       botClient(['Inventory','Store',rootnode,nodes,values],Y),
%	     findall(S,(cliCol(Y,Z),cliGet(Z,'children',GC),
%	     cliCollecton(GC,'children',GCReal),cliToString(GCReal,S)),L),writeq(L).
%

botClient([P|N],Value):-!,botClient(Obj),cliGet(Obj,[P|N],Value).
botClient(Property,Value):-botClient(Obj),cliGet(Obj,Property,Value),!.

% a way to call a method on c#
% cliCall('System',printf(32),Y).
botClientCall(Call):-botClientCall(Call,Res),cliWriteln(Res).
botClientCall([P|N],Value):-!,botClient(Obj),cliGet(Obj,P,Mid),cliGet(Mid,N,Value).
botClientCall(Property,Value):-botClient(Obj),cliCall(Obj,Property,Value).

% wrappered execute command in a convenience pred
% botClientCmd(say("hi"))
%
botClientCmd(In):-botClientCmd(In,Out),cliWriteln(Out),!.
botClientCmd([C|Cmd],Out):-toStringableArgs([C|Cmd],CCmd),!,concat_atom(CCmd,' ',Str),botClientCall(executeCommand(Str),Out).
botClientCmd(C,Out):-compound(C),!,C=..[F|A],listifyFlat(A,FL),!,botClientCmd([F|FL],Out).
% this form expects a term of the form methodname(arg, arg,arg)
%%botClientCmd(Str,Out):-botClientCall(executeCommand(Str),Out).
botClientCmd(Str,Out):-botClientCmd(Str,cliWriteFormat(botClientCmd),Out).
botClientCmd(Str,WriteDelegate,Out):-botClient(C),cliCall(C,executeCommand(Str,C,WriteDelegate),Out).

toStringableArgs(Var,Var):-var(Var),!.
toStringableArgs([C|Cmd],[A|Amd]):-toStringableArg(C,A),toStringableArgs(Cmd,Amd).
toStringableArgs(CCmd,CCmd).

toStringableArg(Var,Var):-var(Var),!,throw(toStringableArgVar(Var)).
toStringableArg(v3d(X,Y,Z),A):-concat_atom([X,Y,Z],'/',A).
toStringableArg(Var,Var).

% helper pred for botClientCmd
listifyFlat([],[]):-!.
listifyFlat([H|T],HT):-!,listifyFlat(H,HL),listifyFlat(T,TL),!,append(HL,TL,HT).
listifyFlat(C,FA):-functor(C,F,1),!,C=..[F,A],!,listifyFlat(A,FA).
listifyFlat(v3d(X,Y,Z),[v3d(X,Y,Z)]).
listifyFlat(v3(X,Y,Z),[v3(X,Y,Z)]).
listifyFlat(C,FA):-compound(C),!,C=..[F|A],!,listifyFlat([F|A],FA).
listifyFlat(C,[C]).

%% get the gridClient Instance
%  libOMV's version of gridClient, in case you want the direct one
gridClient(Obj):-botClient(BC),cliGet(BC,'gridClient',Obj).


%------------------------------------------------------------------------------
% create a writeline delegate
%------------------------------------------------------------------------------
createWritelnDelegate(WID):-createWritelnDelegate(cliWriteFormat(cogrobot),WID).
createWritelnDelegate(WriteDelegate,WID):-cliNewDelegate('MushDLR223.ScriptEngines.OutputDelegate',WriteDelegate,WID).


%------------------------------------------------------------------------------
% listing functions
%------------------------------------------------------------------------------

listS(P):-call(P,A),cliToString(A,S),writeq(S),fail.
listS(P):-writeq('done'(P)),nl.

listAvatars:-listS(simAvatar).
listPrims:-listS(simObject).


%------------------------------------------------------------------------------
% event handler functions
%------------------------------------------------------------------------------
robotToString(C,C):-var(C).
robotToString([],[]).
robotToString([A|B],[AA|BB]):-robotToString(A,AA),robotToString(B,BB).
robotToString(Obj,array(ArrayS)):-Obj='@'(_O), cliIsType(Obj,'System.Array'),cliArrayToTermList(Obj,Array),!,robotToString(Array,ArrayS).
robotToString(Obj,list(ArrayS)):-Obj='@'(_O), cliIsType(Obj,'System.Collections.Generic.IList'('cogbot.NamedParam')),cliCall(Obj,'ToArray',[],Array),robotToString(Array,ArrayS).
robotToString(Obj,enumr(ArrayS)):-Obj='@'(_O), cliIsType(Obj,'System.Collections.IEnumerable'),cliArrayToTermList(Obj,Array),robotToString(Array,ArrayS).
robotToString(C,AS):-compound(C),C=..[F|Args],not(member(F,['@'])),robotToString(Args,ArgS),AS=..[F|ArgS].
robotToString(C,AS):-cliToString(C,AS).

nop(_).


%% print some events
%%onSimEvent(_A,_B,_C):-!. % comment out this first line to print them
:-dynamic(wasSimEvent/3).
onSimEvent(A,B,C):-contains_var("On-Log-Message",a(B,C)),!.
onSimEvent(A,B,C):-contains_var('DATA_UPDATE',a(B,C)),!.
onSimEvent(A,B,C):-!,nop(assertz(wasSimEvent(A,B,C))),!,robotToString(C,AS),!,writeq(onSimEvent(AS)),nl.


%% clearSimEvent(NumToLeave):- predicate_property(wasSimEvent(_,_,_),number_of_clauses(N)),Remove is N-Num, (Remove<=0->true;( ... )).
clearSimEvent(Num):- predicate_property(wasSimEvent(_,_,_),number_of_clauses(N)),Num>=N,!.
clearSimEvent(Num):- retract(wasSimEvent(_,_,_)),predicate_property(wasSimEvent(_,_,_),number_of_clauses(N)),Num >= N,!.
clearSimEvent(_Num).

%% Every minute trim EventLog to 1000 entries
clearEvents:-repeat,sleep(60),clearSimEvent(1000),fail.
:-atInit(thread_create(clearEvents,_,[])).

%%:-module_transparent(onFirstBotClient/2).

%% on first bot client created register the global event handler
user:onFirstBotClient(A,B):-
 botClient(Obj),
  % uncomment the next line if you want all commands to run thru the universal event handler
   cliAddEventHandler(Obj,'EachSimEvent',onSimEvent(_,_,_)),
   cliToString(onFirstBotClient(A-B-Obj),Objs),writeq(Objs),nl.

%% register onFirstBotClient
registerOnFirstBotClient:- cliAddEventHandler('cogbot.ClientManager','BotClientCreated',onFirstBotClient(_,_)).
:-atInit(registerOnFirstBotClient).

%------------------------------------------------------------------------------
% start Radegast!
%------------------------------------------------------------------------------
:-dynamic(ranSL).

runSL:-ranSL,!.
% this is so you can reconsult this file without restarting radegast
runSL:-asserta(ranSL),!,
   cliSet('MushDLR223.Utilities.DLRConsole','NoConsoleVisible','@'(true)),
   cliSet('ABuildStartup.Program','UseApplicationExit','@'(false)),
   cliSet('cogbot.ClientManager','noGUI','@'(true)),
   cliCall('ABuildStartup.Program','Main',[],_).

% assertIfNew is assert a new grounded atomic fact only if the predicate
% was previously undefined

%:-retractall(cliSubProperty(_,_)).
:-assertIfNew(cliSubProperty('cogbot.TheOpenSims.SimAvatar','ProfileProperties')).
:-assertIfNew(cliSubProperty('cogbot.TheOpenSims.SimAvatar','AvatarInterests')).
:-assertIfNew(cliSubProperty('cogbot.TheOpenSims.SimAvatar','FriendshipInfo')).
:-assertIfNew(cliSubProperty('cogbot.TheOpenSims.SimObject','Prim')).
:-assertIfNew(cliSubProperty('cogbot.TheOpenSims.SimObject','Properties')).


obj2Npl(O,npl(66,O)).
npl2Obj(npl(66,O),O).

registerNamedParamRecomposer:-cliToFromRecomposer('System.Collections.Generic.IList'('cogbot.NamedParam'),'npl'(_,_),obj2Npl,npl2Obj).

:-atInit(registerNamedParamRecomposer).

%------------------------------------------------------------------------------
% CLR Introspection of event handlers
%------------------------------------------------------------------------------
simAsset(Asset):-  cliGet('cogbot.TheOpenSims.SimAssetStore','SimAssets',Assets),cliCol(Assets,Asset).


gridClientEvents(E):-cliMemb('OpenMetaverse.GridClient',f,M),arg(3,M,Type),cliMemb(Type,e,E).


listMembs:-cliNew('System.Collections.Generic.List'(string),[int],[10],O),cliMembers(O,M),member(E,M),writeq(E),nl,fail.
listMembs:-gridClientEvents(E),writeq(E),nl,fail.
listMembs. % so pred doesnt fail

%%:-listMembs.

% coerces anything to avatar object
resolveAvatar(Name,Name):-cliIsObject(Name),cliIsType(Name,'SimAvatar'),!.
resolveAvatar(Name,Object):-cliIsObject(Name),cliToString(Name,String),!,resolveAvatar(String,Object).
resolveAvatar(Name,Object):-cliCall('cogbot.Listeners.WorldObjects','GetSimAvatarFromNameIfKnown'(string),[Name],Object).

%% resolveObjectByName(start_hill_walk,O),simObjectColor(O,C),cliWriteln(C).
%% cliCall(static('cogbot.TheOpenSims.SimImageUtils'),'ToNamedColors'('OpenMetaverse.Color4'),[struct('Color4',1,0,1,0)],Named),cliCol(Named,NamedE),cliWriteln(NamedE).
simObjectColor(A,NamedE):-simObject(A),cliCall(static('cogbot.TheOpenSims.SimImageUtils'),'ToNamedColors'('cogbot.TheOpenSims.SimObject'),[A],Named),cliCol(Named,NamedE).
simObjectColor(A,NamedE):-fail,simObject(A),cliGet(A,textures,B),cliGet(B,faceTextures,C),cliCol(C,E),E\=='@'(null),cliGet(E,rgba,CC),
  cliCall(static('cogbot.TheOpenSims.SimImageUtils'),'ToNamedColors'('OpenMetaverse.Color4'),[CC],Named),cliCol(Named,NamedE).

/*

38 ?- simAsset(A),cliGet(A,assetType, enum('AssetType', 'Texture')),cliGet(A,id,UUID),cliGet(A,assetData,Data),Data\=='@'(null), cliCall('OpenMetaverse.Imaging.OpenJPEG','DecodeToImage'(Data,O1,O2),_),cliGetType(O2,T),cliWriteln(T).
"System.Drawing.Bitmap"
A = @'C#664147632',
UUID = uuid("38b86f85-2575-52a9-a531-23108d8da837"),
Data = @'C#664150288',
O1 = @'C#664150368',
O2 = @'C#664150128',
T = @'C#664150520' .

*/

%%b2img('@'(null),'@'(null)):-!.
b2img(Data,Image):-Data\=='@'(null),cliCall('OpenMetaverse.Imaging.OpenJPEG','DecodeToImage'(Data,_O1,Image),_).

textureIDToImage(UUID,Image):-nonvar(UUID),!,cliCall('cogbot.Listeners.WorldObjects',['GridMaster','TextureBytesForUUID'(UUID)],Bytes),b2img(Bytes,Image).
textureIDToImage(UUID,Image):-var(UUID),!,simAsset(A),cliGet(A,assetType, enum('AssetType', 'Texture')),cliGet(A,id,UUID),cliGet(A,assetData,Data),b2img(Data,Image).
textureIDToImageParts(UUID,Part):-simAsset(A),cliGet(A,assetType, enum('AssetType', 'Texture')),cliGet(A,id,UUID),cliGet(A,imageStats,Parts),cliArrayToTermList(Parts,List),List=[_|_],member(Part,List).


requestTexture(UUID):-worldSystem(Sys),cliCall(Sys,'StartTextureDownload'(UUID),_O).

resolveObjectByName(Name,Object):-cliCall('cogbot.Listeners.WorldObjects','GetSimPositionByName'(string),[Name],Object).

sayTo(Speaker,ToWho,What):-resolveAvatar(ToWho,Listener),cliCall(Speaker,talkto('SimAvatar',string),[Listener,What],_O).

% gives you a list of all the properties
simObject(X,OE):-simObject(X),cliGet(X,infoMap,Y),cliCol(Y,PE),cliUnify(OE,PE).

:-set_prolog_flag(double_quotes,string).
%------------------------------------------------------------------------------
%------------------------------------------------------------------------------
simDistance(V1,V2,D):-cliCall(V1,distance(V1,V2),D).

simAvDistance(A,C,E):-var(A),nonvar(C),!,simAvDistance(C,A,E).
simAvDistance(A,C,E):-simAvatar(A),cliGet(A,globalposition,B),simAvatar(C),A\=C,cliGet(C,globalposition,D),simDistance(B,D,E).
simObjDistance(A,C,E):-var(A),nonvar(C),!,simObjDistance(C,A,E).
simObjDistance(A,C,E):-simObject(A),cliGet(A,globalposition,B),simObject(C),A\=C,cliGet(C,globalposition,D),simDistance(B,D,E).
%------------------------------------------------------------------------------
%------------------------------------------------------------------------------


%------------------------------------------------------------------------------
% Position/Vectort calls
%
%------------------------------------------------------------------------------
vectorAdd(A1,A2,R):-cliCall(A1,add(A1,A2),R).

% already global vect!
toGlobalVect(Vect,Vect):-functor(Vect,v3d,3),!.
toGlobalVect(v3(A,B,C),Vect):-botClient(['Network','CurrentSim','Handle'],S),cliCall('SimRegion','HandleLocalToGlobal'(S,v3(A,B,C)),Vect),!.
%% ?- toGlobalVect('annies haven/129.044327/128.206070/81.519630',D).
toGlobalVect(A,Vect):-atom(A),concat_atom([R,X,Y,Z|_],'/',A),!,gridClient(BC),cliCall('SimRegion','GetRegionByName'(R,BC),Reg),cliCall(Reg,'LocalToGlobal'(v3(X,Y,Z)),Vect).
%% ?- toGlobalVect('129.044327/128.206070/81.519630',D).
toGlobalVect(A,Vect):-atom(A),concat_atom([X,Y,Z],'/',A),!,toGlobalVect(v3(X,Y,Z),Vect).
%% ?- toGlobalVect('CyberPunk Buddha - L',D).
toGlobalVect(A,Vect):-atom(A),!,resolveObjectByName(A,Obj),cliGet(Obj,globalposition,Vect),!.
toGlobalVect(Obj,Vect):-cliGet(Obj,globalposition,Vect),!.

toLocalVect(Obj,LV):-toGlobalVect(Obj,Vect),cliCall('SimRegion','GlobalToLocalStatic'(Vect),LV).

%% 
distanceTo(A,R):-toGlobalVect(A,A2),!,botClient(['Self','GlobalPosition'],A1),cliCall(A2,distance(A1,A2),R).

% ?- moveTo('CyberPunk Buddha - L',4,FD).

moveTo(Dest,Time,FDist):-botClientCmd(moveto(Dest)),botClientCmd(waitpos(Time,Dest)),botClientCmd(stopMoving),distanceTo(Dest,FDist).

chat(Msg):-chat(Msg,0).
chat(Msg,Ch):-chat(Msg,Ch,'Normal').
chat(Msg,Ch,Type):-botClient(X),cliCall(X,talk(Msg,Ch,Type),_).

%%:-prev_dir6(X),cd(X).
