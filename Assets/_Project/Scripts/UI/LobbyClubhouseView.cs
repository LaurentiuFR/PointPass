using System;
using PointPass.Core;
using UnityEngine;
using static PointPass.UI.LobbyUi;

namespace PointPass.UI
{
    public partial class LobbyMenuView
    {
        public static readonly Color ClubhouseNavy = new Color(9f/255f, 21f/255f, 37f/255f);
        private static readonly Color ClubhousePanel = new Color(15f/255f,34f/255f,55f/255f);
        private static readonly Color ClubhouseTile = new Color(21f/255f,45f/255f,70f/255f);
        private static readonly Color ClubhouseIce = new Color(155f/255f,221f/255f,246f/255f);
        private static readonly Color ClubhouseMuted = new Color(157f/255f,176f/255f,195f/255f);
        private RectTransform clubhouseHeader;

        private static Vector2 At(float x, float y, float width, float height) => new Vector2(x+width*.5f-800,450-y-height*.5f);
        private static UnityEngine.UI.Text Copy(Transform parent,string name,string label,int font,float x,float y,float width,float height,Color? tint=null,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft)
            => Text(parent,name,label,font,At(x,y,width,height),new Vector2(width,height),tint??ClubhouseMuted,bold,align);
        private static UnityEngine.UI.Image Plate(Transform parent,string name,float x,float y,float width,float height,Color? tint=null)
            => Panel(parent,name,At(x,y,width,height),new Vector2(width,height),tint??ClubhousePanel,false);
        private static UnityEngine.UI.Button Control(Transform parent,string name,string label,float x,float y,float width,float height,Action action,bool primary=false)
        {
            var button=Button(parent,name,label,At(x,y,width,height),new Vector2(width,height),action,primary);
            var image=button.GetComponent<UnityEngine.UI.Image>();
            image.color=primary?ClubhouseIce:ClubhouseTile;
            // Keep the reference's visual proportions while giving short controls a generous touch area.
            if(height<100) image.raycastPadding=new Vector4(0,-(104-height)*.5f,0,-(104-height)*.5f);
            return button;
        }

        private void BuildHeader()
        {
            clubhouseHeader=Rect(transform,"GlobalHeader",Vector2.zero,new Vector2(1600,900));
            var profile=Plate(clubhouseHeader,"PlayerProfile",48,36,270,64);
            Icon(profile.transform,"Avatar",LobbyIcon.Symbol.User,new Vector2(-109,0),30,ClubhouseIce);
            Text(profile.transform,"DisplayName",Profile.DisplayName,22,new Vector2(36,12),new Vector2(230,30),MatchPalette.Paper,true,TextAnchor.MiddleLeft);
            Text(profile.transform,"Rank","Local profile · Rookie",17,new Vector2(36,-15),new Vector2(230,26),ClubhouseMuted,false,TextAnchor.MiddleLeft);
            wordmark=Copy(clubhouseHeader,"Wordmark","POINT  <color=#9BDDF6>PASS</color>",34,622,45,320,52,MatchPalette.Paper,true);
            var setting=Control(clubhouseHeader,"Settings","",1082,38,122,60,()=>ShowPage(Page.Settings));
            setting.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
            Icon(setting.transform,"Gear",LobbyIcon.Symbol.Gear,new Vector2(-42,0),27,ClubhouseIce);
            Text(setting.transform,"SettingsLabel","Settings",21,new Vector2(22,0),new Vector2(90,40),ClubhouseMuted);
            var coinPlate=Plate(clubhouseHeader,"CoinWallet",1244,40,146,56);
            Icon(coinPlate.transform,"Coin",LobbyIcon.Symbol.Coin,new Vector2(-51,0),23,MatchPalette.Gold);
            coins=Text(coinPlate.transform,"MatchCoins","",24,new Vector2(18,10),new Vector2(94,30),MatchPalette.Paper,true,TextAnchor.MiddleLeft);
            Text(coinPlate.transform,"Caption","Match Coins",13,new Vector2(18,-14),new Vector2(94,20),ClubhouseMuted,false,TextAnchor.MiddleLeft);
            var pointPlate=Plate(clubhouseHeader,"PointWallet",1406,40,146,56);
            Icon(pointPlate.transform,"Points",LobbyIcon.Symbol.Point,new Vector2(-51,0),23,ClubhouseIce);
            points=Text(pointPlate.transform,"PassPoints","",24,new Vector2(18,10),new Vector2(94,30),MatchPalette.Paper,true,TextAnchor.MiddleLeft);
            Text(pointPlate.transform,"Caption","Pass Points",13,new Vector2(18,-14),new Vector2(94,20),ClubhouseMuted,false,TextAnchor.MiddleLeft);
            backToLobby=Control(clubhouseHeader,"BackToLobby","← BACK",48,48,160,48,()=>ShowPage(Page.MainLobby));
            backToLobby.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=20;
        }

        private void RefreshClubhouseHeader(bool home)
        {
            if(clubhouseHeader==null) return;
            foreach(Transform child in clubhouseHeader)
                child.gameObject.SetActive(child.name=="BackToLobby"?!home:child.name=="Wordmark"||home);
            wordmark.rectTransform.anchoredPosition=home?At(622,45,320,52):At(224,45,320,52);
        }

        private void BuildLobby()
        {
            Transform page=pages[Page.MainLobby].transform;
            ((RectTransform)page).sizeDelta=new Vector2(1600,900);
            Copy(page,"ClubhouseHeading","YOUR CLUBHOUSE",17,48,126,360,28,ClubhouseMuted,true);
            ClubhouseAction(page,"Shop","Shop","Fresh kits &\ncosmetics",LobbyIcon.Symbol.Bag,48,168,()=>ShowPage(Page.ItemShop));
            ClubhouseAction(page,"MyClub","My Club","Your colours. Your\ncrew.",LobbyIcon.Symbol.Shield,236,168,()=>ShowPage(Page.PlayerHub));
            ClubhouseAction(page,"PassingPractice","Practice","Sharpen your passing",LobbyIcon.Symbol.Pitch,48,354,()=>host.RequestPassingPractice());
            ClubhouseAction(page,"Leaderboards","Leaderboards","Chase the top spot",LobbyIcon.Symbol.Trophy,236,354,()=>OpenDialog("LEADERBOARDS","Online rankings are a preview. Results will appear when multiplayer services are connected.","BACK TO LOBBY",CloseModal),MatchPalette.Gold);
            Plate(page,"SeasonCard",48,540,360,128);
            Copy(page,"SeasonLabel","SEASON 01 / THE KICK-OFF",17,68,556,320,30,ClubhouseIce,true);
            Copy(page,"SeasonTitle","Build your game.",29,68,590,320,38,MatchPalette.Paper,true);
            Copy(page,"SeasonDescription","Play together. Climb together.",19,68,630,320,30);
            Copy(page,"HeroEyebrow","THE BEAUTIFUL GAME. YOUR WAY.",16,600,126,328,28,ClubhouseMuted,true,TextAnchor.MiddleCenter);
            var actor=Rect(page,"ClubhouseFootballer",At(576,200,376,461),new Vector2(376,461));
            actor.gameObject.AddComponent<ClubhousePlayerGraphic>();
            Text(actor,"ShirtNumber","10",43,new Vector2(0,61.5f),new Vector2(50,60),new Color(9f/255f,26f/255f,42f/255f),true);
            Copy(page,"PlayerName",Profile.DisplayName+"  <color=#9BDDF6>/ YOU</color>",23,594,710,340,40,MatchPalette.Paper,true,TextAnchor.MiddleCenter);
            Copy(page,"PartyHeading","YOUR PARTY",17,1120,126,320,28,ClubhouseMuted,true);
            partyCount=Copy(page,"PartyCount","1 / 5",18,1472,126,80,28,ClubhouseIce,true,TextAnchor.MiddleRight);
            var invite=Control(page,"InviteFriends","",1120,194,432,280,OpenParty);
            invite.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
            Image(invite.transform,"InviteDisc",new Vector2(0,40),Vector2.one*104,ClubhousePanel,SpriteLibrary.WhiteCircle());
            Icon(invite.transform,"Plus",LobbyIcon.Symbol.Plus,new Vector2(0,40),38,ClubhouseIce);
            Text(invite.transform,"InviteHeading","BETTER WITH FRIENDS",28,new Vector2(0,-44),new Vector2(432,42),MatchPalette.Paper,true);
            Text(invite.transform,"InviteDescription","Invite your crew or jump in solo.",21,new Vector2(0,-94),new Vector2(432,40),ClubhouseMuted);
            Image(page,"RegionDot",At(1120,532,11,11),Vector2.one*11,new Color(.53f,.85f,.71f),SpriteLibrary.WhiteCircle());
            Copy(page,"Region","EUROPE",17,1142,522,250,32,ClubhouseMuted,true);
            Copy(page,"ConnectionState","LOCAL PREVIEW",16,1410,522,142,32,new Color(.53f,.85f,.71f),false,TextAnchor.MiddleRight);
            homePlay=Control(page,"Play","PLAY",1120,586,432,72,()=>ShowPage(Page.GameModes),true);
            homePlay.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=32;
            Icon(homePlay.transform,"Arrow",LobbyIcon.Symbol.Arrow,new Vector2(-92,0),24,ClubhouseNavy);
            Copy(page,"PlayHint","Choose your match on the next screen",18,1120,680,432,32);
            var how=Control(page,"HowToPlay","HOW TO PLAY",1120,726,432,48,ShowGuide);
            how.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=21;
            Image(page,"FriendsDot",At(48,818,14,14),Vector2.one*14,new Color(.53f,.85f,.71f),SpriteLibrary.WhiteCircle());
            Copy(page,"FriendsPreview","FRIENDS · PREVIEW",17,70,808,380,32,new Color(.53f,.85f,.71f));
            Copy(page,"Principle","ONE PLAYER. EVERY PASS MATTERS.",17,600,808,420,32,ClubhouseMuted,false,TextAnchor.MiddleCenter);
            Copy(page,"SeasonFooter","POINTPASS / SEASON 01",16,1330,808,222,32,ClubhouseMuted,false,TextAnchor.MiddleRight);
        }

        private void ClubhouseAction(Transform parent,string name,string label,string detail,LobbyIcon.Symbol icon,float x,float y,Action action,Color? tint=null)
        {
            var button=Control(parent,name,"",x,y,172,166,action);
            button.GetComponent<UnityEngine.UI.Image>().color=ClubhousePanel;
            Icon(button.transform,"Icon",icon,new Vector2(-50,47),32,tint??ClubhouseIce);
            Text(button.transform,"ActionLabel",label,26,new Vector2(0,3),new Vector2(132,40),MatchPalette.Paper,true,TextAnchor.MiddleLeft);
            Text(button.transform,"Description",detail,17,new Vector2(0,-43),new Vector2(132,56),ClubhouseMuted,false,TextAnchor.UpperLeft);
        }

        private void BuildModes()
        {
            Transform page=pages[Page.GameModes].transform;
            ((RectTransform)page).sizeDelta=new Vector2(1600,900);
            Plate(page,"MatchSelector",48,136,1504,650);
            Copy(page,"PageTitle","CHOOSE YOUR MATCH",43,80,167,1440,65,MatchPalette.Paper,true);
            Copy(page,"PageDescription","Select a team size. Custom lobbies let you play by your own rules.",25,80,242,1440,46);
            string[] roles={"DUEL","DUO","TRIO","SQUAD","FULL TEAM"};
            for(int i=0;i<5;i++)
            {
                int mode=i+1;
                var card=Control(page,"MatchFormat"+mode,"",80+i*291,298,275,280,()=>
                {
                    SetSquadSize(mode);
                    OpenDialog(mode+"v"+mode+" / "+roles[mode-1],"Multiplayer preview. No online session is created. Use the local challenge to play the current game.","FIND MATCH · PREVIEW",()=>OpenDialog("FINDING YOUR MATCH","Searching preview only. No live connection is made.","CANCEL SEARCH",CloseModal));
                });
                Icon(card.transform,"Player",LobbyIcon.Symbol.User,new Vector2(-94,96),32,ClubhouseIce);
                Text(card.transform,"Format",mode+"v"+mode,58,new Vector2(0,31),new Vector2(227,80),MatchPalette.Paper,true,TextAnchor.MiddleLeft);
                Text(card.transform,"Role",roles[i],22,new Vector2(0,-42),new Vector2(227,36),ClubhouseIce,true,TextAnchor.MiddleLeft);
                Text(card.transform,"SelectMode","Select mode →",18,new Vector2(0,-89),new Vector2(227,34),ClubhouseMuted,false,TextAnchor.MiddleLeft);
            }
            Control(page,"MatchFormat6","CUSTOM LOBBIES",80,602,440,48,()=>{SetSquadSize(5);OpenDialog("CUSTOM LOBBIES","Private rooms and join codes are a preview. Add named preview guests from Invite Friends on the main menu.","BACK TO LOBBY",()=>ShowPage(Page.MainLobby));});
            Copy(page,"OnlineDisclosure","Prototype preview · Matchmaking does not connect to a server.",20,80,670,1100,42);
            var local=Control(page,"PlayOffline","PLAY LOCAL KEEPER CHALLENGE",1030,696,490,58,()=>host.RequestOfflineMatch(difficulty));
            local.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=23;
        }
    }
}
