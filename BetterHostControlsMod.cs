using System;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using RainMeadow;
using BepInEx;
using MonoMod.RuntimeDetour;
using Menu;
using static RainMeadow.RainMeadow;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]

namespace BetterHostControls;

[BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.HardDependency)]

[BepInPlugin(MOD_ID, MOD_NAME, MOD_VERSION)]
public partial class BetterHostControlsMod : BaseUnityPlugin
{
    public const string MOD_ID = "LazyCowboy.BetterHostControls";
    public const string MOD_NAME = "Better Host Controls";
    public const string MOD_VERSION = "0.0.1";

    #region Setup
    public BetterHostControlsMod()
    {
    }

    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }

    private void OnDisable()
    {
        if (IsInit)
        {
            PlayerButtonHook.Dispose();
        }
    }

    Hook RegionGateHook;
    Hook PlayerButtonHook;

    private bool IsInit;
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        try
        {
            if (IsInit) return;

            //Your hooks go here
            RegionGateHook = new Hook(
                typeof(RegionGate).GetProperty(nameof(RegionGate.MeetRequirement)).GetGetMethod(),
                RegionGate_MeetRequirement_Hook
            );

            PlayerButtonHook = new Hook(
                typeof(SpectatorOverlay.PlayerButton).GetConstructors()[0],
                PlayerButtonCtorHook
            );

            IsInit = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }
    #endregion

    #region GateKicking
    public bool RegionGate_MeetRequirement_Hook(orig_RegionGateBool orig, RegionGate self)
    {
        bool ret = orig(self);
        try
        {
            if (ret && isStoryMode(out var storyGameMode))
            {
                //kick all dead players in lobby
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    //determine if player should be kicked
                    bool shouldKick = true;
                    //if the player can be found as alive, no; else, yes
                    foreach (var avatar in OnlineManager.lobby.playerAvatars)
                    {
                        if (avatar.Key == player)
                        {
                            if (avatar.Value.FindEntity() is OnlinePhysicalObject opo && opo.apo.realizedObject is Player p)
                            {
                                shouldKick = p.dead;
                            }
                            break;
                        }
                    }
                    if (shouldKick)
                        player.InvokeRPC(StoryRPCs.GoToWinScreen, false, null);
                }
            }
        } catch (Exception ex) { Logger.LogError(ex); }
        return ret;
    }
    #endregion

    #region ExplodeButton
    public delegate void PlayerButtonCtorOrig(SpectatorOverlay.PlayerButton self, SpectatorOverlay menu, OnlinePhysicalObject opo, Vector2 pos, bool canKick);
    public void PlayerButtonCtorHook(PlayerButtonCtorOrig orig, SpectatorOverlay.PlayerButton self, SpectatorOverlay menu, OnlinePhysicalObject opo, Vector2 pos, bool canKick)
    {
        orig(self, menu, opo, pos, canKick);

        try
        {
            if (canKick)
            {
                RemoveKickButton(self);

                //Rain Meadow currently adds two buttons: the kick button, and THEN the mute button on top of it.
                //So I have to filter through to find the kick button. ...fun...
                SimplerSymbolButton kickButton2 = null;
                foreach (MenuObject item in self.kickbutton.owner.subObjects)
                {
                    if (item is SimplerSymbolButton button2 && button2.signalText == "KICKPLAYER")
                    {
                        kickButton2 = button2;
                        break;
                    }
                }
                if (kickButton2 != null)
                {
                    kickButton2.RemoveSprites();
                    kickButton2.page.RemoveSubObject(kickButton2);
                    Logger.LogDebug("Removed second kick button");
                }

                //if (opo.apo.realizedObject is Player player && !player.dead)
                    AddExplodeButton(self, pos);
                //else
                    //AddDeathScreenButton(self, pos);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private void RemoveKickButton(SpectatorOverlay.PlayerButton self)
    {
        self.kickbutton.RemoveSprites();
        self.kickbutton.page.RemoveSubObject(self.kickbutton);
        Logger.LogDebug("Removed kick button");
    }

    private void AddKickButton(SpectatorOverlay.PlayerButton self, Vector2 pos)
    {
        //copied from Rain Meadow code...
        self.kickbutton = new SimplerSymbolButton(self.overlay, self.overlay.pages[0], "Menu_Symbol_Clear_All", "KICKPLAYER", pos + new Vector2(120, 0));
        self.kickbutton.OnClick += (_) => BanHammer.BanUser(self.player.owner);
        self.kickbutton.owner.subObjects.Add(self.kickbutton);
        Logger.LogDebug("Added kick button");
    }

    private void AddDeathScreenButton(SpectatorOverlay.PlayerButton self, Vector2 pos)
    {
        self.kickbutton = new SimplerSymbolButton(self.overlay, self.overlay.pages[0], "Kill_Slugcat", "KICKPLAYER", pos + new Vector2(120, 0));
        self.kickbutton.OnClick += (_) =>
        {
            if (self.player.apo.realizedObject is Player)
            {
                //opo.owner.InvokeRPC(opo.Explode, Vector2.zero);
                self.player.owner.InvokeRPC(StoryRPCs.GoToDeathScreen);
                Logger.LogDebug("Hurt player");
            }
            else Logger.LogDebug("Player not found");

            //switch to kick button
            RemoveKickButton(self);
            AddKickButton(self, pos);
        };
        self.kickbutton.owner.subObjects.Add(self.kickbutton);
        Logger.LogDebug("Set up Remove-to-lobby button");
    }

    private void AddExplodeButton(SpectatorOverlay.PlayerButton self, Vector2 pos)
    {
        self.kickbutton = new SimplerSymbolButton(self.overlay, self.overlay.pages[0], "Kill_Slugcat", "KICKPLAYER", pos + new Vector2(120, 0));
        self.kickbutton.OnClick += (_) =>
        {
            if (self.player.apo.realizedObject is Player)
            {
                self.player.owner.InvokeRPC(self.player.Explode, Vector2.zero);
                //self.player.owner.InvokeRPC(StoryRPCs.GoToDeathScreen);
                Logger.LogDebug("Hurt player");
            }
            else Logger.LogDebug("Player not found");

            //switch to death screen button
            RemoveKickButton(self);
            //AddDeathScreenButton(self, pos);
            AddKickButton(self, pos);
        };
        self.kickbutton.owner.subObjects.Add(self.kickbutton);
        Logger.LogDebug("Set up Remove-to-lobby button");
    }
    #endregion
}