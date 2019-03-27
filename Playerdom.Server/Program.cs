﻿//using LiteNetLib;
//using LiteNetLib.Utils;
//using MessagePack;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Playerdom.Shared.Objects;
using Playerdom.Shared.Services;
using Playerdom.Shared;
using Playerdom.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Net.Sockets;
using System.Reflection;
using Ceras;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Playerdom.Server
{
    public class Program
    {

        public static ConcurrentQueue<string> leavingPlayers = new ConcurrentQueue<string>();
        public static Map level = new Map();
        public static ConcurrentDictionary<string, ServerClient> clients = new ConcurrentDictionary<string, ServerClient>();

        static void AcceptClients()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 25565);
            listener.Start();

            while (true)
            {
                TcpClient tcpClient = listener.AcceptTcpClient();

                ServerClient sc = new ServerClient(tcpClient);
                clients.TryAdd(sc.EndPointString, sc);
            }

        }

        static void UpdateAll()
        {
            while(true)
            {
                foreach(KeyValuePair<string, ServerClient> sc in clients)
                {
                    if(!sc.Value.IsInitialized)
                    {
                        sc.Value.InitializePlayer();
                    }
                    if(sc.Value.LastUpdate.AddSeconds(5) > DateTime.Now)
                    {
                        leavingPlayers.Enqueue(sc.Value.EndPointString);
                        sc.Value.RemovePlayer();
                    }
                }

                while(leavingPlayers.TryDequeue(out string endpoint))
                {
                    clients.TryRemove(endpoint, out ServerClient sc);
                }
                leavingPlayers.Clear();


                GameTime gameTime = new GameTime();
                foreach (KeyValuePair<Guid, Entity> ent in level.gameEntities)
                {
                    ent.Value.Update(gameTime, level);
                }

                foreach (KeyValuePair<Guid, GameObject> g in level.gameObjects)
                {
                    if (!clients.Any(cl => cl.Value.FocusedObjectID == g.Key))
                        g.Value.Update(gameTime, level, new KeyboardState());
                    else g.Value.Update(gameTime, level, clients.First(cl => cl.Value.FocusedObjectID == g.Key).Value.InputState);
                }
                foreach (KeyValuePair<Guid, GameObject> g1 in level.gameObjects)
                {
                    foreach (KeyValuePair<Guid, GameObject> g2 in level.gameObjects)
                    {
                        if (g1.Value != g2.Value)
                        {
                            if (g1.Value.CheckCollision(g2.Value))
                            {
                                g1.Value.HandleCollision(g2.Value, level);
                                g2.Value.HandleCollision(g1.Value, level);

                            }
                        }
                    }
                    foreach (KeyValuePair<Guid, Entity> ent in level.gameEntities)
                    {
                        Vector2 depth = CollisionService.GetIntersectionDepth(g1.Value.BoundingBox, ent.Value.BoundingBox);

                        if (depth.X != 0 && depth.Y != 0)
                        {
                            g1.Value.HandleCollision(ent.Value, level);
                        }
                    }
                }
                foreach (KeyValuePair<Guid, GameObject> o in level.gameObjects)
                {
                    if (o.Value.MarkedForDeletion)
                    {
                        level.objectsMarkedForDeletion.Add(o.Key);
                    }
                }
                foreach (KeyValuePair<Guid, Entity> ent in level.gameEntities)
                {
                    if (ent.Value.MarkedForDeletion)
                    {
                        level.entitiesMarkedForDeletion.Add(ent.Key);
                    }
                }

                foreach (Guid g in level.objectsMarkedForDeletion)
                {
                    level.gameObjects.Remove(g, out GameObject o);
                }
                level.objectsMarkedForDeletion.Clear();

                foreach (Guid g in level.entitiesMarkedForDeletion)
                {
                    level.gameEntities.Remove(g, out Entity o);
                }
                level.entitiesMarkedForDeletion.Clear();


                Thread.Sleep(30);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Playerdom Test Server");
            PlayerdomCerasSettings.Initialize();



            level = MapService.CreateMap("World");


            new Thread(() => AcceptClients()).Start();
            new Thread(() => UpdateAll()).Start();



            while (!Console.KeyAvailable)
            {
            }
        }
    }
}
