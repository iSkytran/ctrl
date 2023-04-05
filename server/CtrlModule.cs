using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

namespace Celeste.Mod.Ctrl
{
    public class CtrlModule : EverestModule
    {

        public static CtrlModule instance;

        private ResponseSocket server;
        private IDetour hookTick;
        private bool playerReady;
        private List<string> roomsVisited;
        private List<int> inputFrame;
        private int reward;
        private Vector2 prevCenter;
        private int idleCount;
        private bool terminated;

        public CtrlModule()
        {
            instance = this;
            server = null;
            hookTick = null;
            playerReady = false;
            roomsVisited = new List<string>();
            inputFrame = null;
            reward = -1;
            prevCenter = new Vector2(0);
            idleCount = 0;
            terminated = false;
        }

        public override void Load()
        {
            On.Monocle.Engine.RunWithLogging += GameRun;
            hookTick = new Hook(
                typeof(Game).GetMethod("Tick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
                typeof(CtrlModule).GetMethod("GameTick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            );
            On.Celeste.Celeste.Update += GameUpdate;
            On.Celeste.Player.Update += PlayerUpdate;
            On.Monocle.MInput.KeyboardData.Update += InputUpdate;
            Everest.Events.Player.OnSpawn += OnSpawn;
            Everest.Events.Player.OnDie += OnDie;
            Everest.Events.Level.OnTransitionTo += OnTransitionTo;
        }

        public override void Unload()
        {
            On.Monocle.Engine.RunWithLogging -= GameRun;
            hookTick?.Dispose();
            On.Celeste.Celeste.Update -= GameUpdate;
            On.Celeste.Player.Update -= PlayerUpdate;
            On.Monocle.MInput.KeyboardData.Update -= InputUpdate;
            Everest.Events.Player.OnSpawn -= OnSpawn;
            Everest.Events.Player.OnDie -= OnDie;
            Everest.Events.Level.OnTransitionTo -= OnTransitionTo;

        }

        public delegate void orig_Tick(Game self);
        public static void GameTick(orig_Tick orig, Game self)
        {
            DynamicData game = new DynamicData(self);
            game.Set("IsFixedTimeStep", false);
            orig(self);
        }

        private void OnSpawn(Player player)
        {
            prevCenter = player.Center;
            playerReady = true;
        }

        private void OnDie(Player player)
        {
            playerReady = false;
            roomsVisited.Clear();
            reward -= 50;
            terminated = true;

            // Reset to first room on death.
            DynamicData cmd = new DynamicData(typeof(Commands));
            cmd.Invoke("CmdLoadIDorSID", "1", "1");
        }

        private void OnTransitionTo(Level level, LevelData next, Vector2 direction)
        {
            // Don't reward revisit of rooms.
            if (!roomsVisited.Contains(next.Name))
            {
                roomsVisited.Add(next.Name);
                reward += 200;
            }
        }

        private void GameRun(On.Monocle.Engine.orig_RunWithLogging orig, global::Monocle.Engine self)
        {
            try
            {
                // Create ZMQ connection for RL agent.
                server = new ResponseSocket();
                server.Bind("tcp://*:7777");
                orig(self);
            }
            finally
            {
                server?.Dispose();
            }
        }

        private void GameUpdate(On.Celeste.Celeste.orig_Update orig, Celeste self, GameTime gameTime)
        {
            // Custom stepping.
            if (playerReady)
            {
                // Read agent command.
                string payload = server.ReceiveFrameString();
                inputFrame = JsonConvert.DeserializeObject<List<int>>(payload);

                // Run update.
                orig(self, gameTime);

                // Send back reward.
                payload = JsonConvert.SerializeObject(new List<int> { reward, terminated ? 1 : 0 });
                server.SendFrame(payload);

                // Reset reward and terminated status.
                reward = -1;
                terminated = false;
            }
            else
            {
                // Regular updates.
                orig(self, gameTime);
            }
        }

        private void PlayerUpdate(On.Celeste.Player.orig_Update orig, global::Celeste.Player self)
        {
            // Naive reward of how much the agent moved toward the top right.
            float deltaX = Convert.ToInt32(self.Center.X - prevCenter.X);
            float deltaY = -Convert.ToInt32(self.Center.Y - prevCenter.Y); // Up is negative.
            int dist = Convert.ToInt32(Math.Sqrt(deltaX * deltaX + deltaY * deltaY)); // Dist is positive.
            if (deltaX <= 0 || deltaY <= 0) {
                // Only give positive rewards for moving up and to the right.
                dist *= -1;
            }
            reward += dist;
            prevCenter = self.Center;

            // If agent doesn't move for 1000 timesteps, terminate.
            if (deltaX == 0 && deltaY == 0)
            {
                if (++idleCount >= 150)
                {
                    idleCount = 0;
                    reward -= 100;
                    terminated = true;
                }
            }

            orig(self);
        }

        private void InputUpdate(On.Monocle.MInput.KeyboardData.orig_Update orig, global::Monocle.MInput.KeyboardData self)
        {
            orig(self);
            if (playerReady && inputFrame != null && inputFrame.Count == 7)
            {
                // Convert agent action to keys pressed.
                List<Keys> keys = new List<Keys>();
                if (inputFrame[0] == 1)
                {
                    keys.Add(Keys.Left);
                }
                if (inputFrame[1] == 1)
                {
                    keys.Add(Keys.Right);
                }
                if (inputFrame[2] == 1)
                {
                    keys.Add(Keys.Up);
                }
                if (inputFrame[3] == 1)
                {
                    keys.Add(Keys.Down);
                }
                if (inputFrame[4] == 1)
                {
                    // Jump
                    keys.Add(Keys.C);
                }
                if (inputFrame[5] == 1)
                {
                    // Dash
                    keys.Add(Keys.X);
                }
                if (inputFrame[6] == 1)
                {
                    // Grab
                    keys.Add(Keys.Z);
                }

                // Press desired keys.
                KeyboardState state = new KeyboardState(keys.ToArray());
                Monocle.MInput.Keyboard.CurrentState = state;

            }
            else if (inputFrame != null && inputFrame.Count == 1)
            {
                // Reset not really needed as this is done on death and next update.
                roomsVisited.Clear();
                terminated = false;

                // Reset to first room on death.
                DynamicData cmd = new DynamicData(typeof(Commands));
                cmd.Invoke("CmdLoadIDorSID", "1", "1");
            }
            // Reset inputFrame.
            inputFrame = null;
        }
    }
}
