﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Playerdom.Shared.Objects;
using Playerdom.Shared.GUIs;
using Playerdom.Shared.Services;
using Playerdom.Shared.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
//using MessagePack;
using LiteNetLib.Utils;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Net;
using System.Reflection;
using Ceras;
//using LiteNetLib;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using Ceras.Helpers;

#if WINDOWS_UAP
using Windows.Storage;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Core;
#elif WINDOWS
using System.Windows;
#endif


namespace Playerdom.Shared
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class GameLevel : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D groundTexture;
        Texture2D grassTexture;
        Texture2D flowerTexture;
        Texture2D stoneTexture;
        Texture2D mossyStoneTexture;
        Texture2D sandyPathTexture;
        Texture2D gravelPathTexture;
        Texture2D waterTexture;
        Texture2D wavyWaterTexture;
        Texture2D bricksTexture;
        Texture2D woodFlooringTexture;

        public static Map level;
        static Assembly asm = Assembly.GetEntryAssembly();
        SpriteFont font;
        SpriteFont font2;

        static TcpClient _tcpClient;
        static NetworkStream _netStream;
        static CerasSerializer _sendCeras;
        static CerasSerializer _receiveCeras;


        List<ButtonObject> buttons = new List<ButtonObject>();

        static KeyValuePair<Guid, GameObject> focusedObject = new KeyValuePair<Guid, GameObject>(Guid.Empty, null);

        const string WATERMARK = "Playerdom Test - Copyright 2019 Dylan Green";

        const ushort VIEW_DISTANCE = 31;

        //static CerasSerializer serializer = new CerasSerializer();
        //static byte[] serializerBuffer = null;

        public GameLevel()
        {
            Window.AllowUserResizing = true;
            graphics = new GraphicsDeviceManager(this);

#if !WINDOWS_UAP
            graphics.PreferredBackBufferWidth = 1920;
            graphics.PreferredBackBufferHeight = 1080;
#endif
            this.IsMouseVisible = true;
            Content.RootDirectory = "Content";

            _tcpClient = new TcpClient();

        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            PlayerdomCerasSettings.Initialize();
            string ip = "localhost";
#if WINDOWS_UAP
            try
            {
                StorageFile file;
                file = Task.Run(async () => await ApplicationData.Current.LocalFolder.GetFileAsync("connection.txt")).Result;

                ip = File.ReadAllText(file.Path);
            }
            catch(Exception e)
            {
                StorageFile file;
                file = Task.Run(async () => await ApplicationData.Current.LocalFolder.CreateFileAsync("connection.txt", CreationCollisionOption.OpenIfExists)).Result;

                File.WriteAllText(file.Path, ip);
                ip = "localhost";
            }


#elif WINDOWS
            try
            {
                ip = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\connection.txt");
            }
            catch (Exception e)
            {
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\connection.txt", ip);
                ip = "localhost";
            }
#endif
            _tcpClient = new TcpClient();
            _tcpClient.Connect("localhost", 25565);
            _netStream = _tcpClient.GetStream();

            _sendCeras = new CerasSerializer(PlayerdomCerasSettings.config);
            _receiveCeras = new CerasSerializer(PlayerdomCerasSettings.config);

            level = new Map();

            level.tiles = new Tile[Map.SIZE_X, Map.SIZE_Y];

            new Thread(() => ReceiveOutputAsync()).Start();


                Stopwatch connectionWatch = new Stopwatch();
                byte attempts = 0;

                connectionWatch.Start();
                while (focusedObject.Value == null)
                {
                    if(attempts > 20)
                    {
                        throw new Exception("Connection Timed Out");
                    }

                    if(connectionWatch.ElapsedMilliseconds > 1000)
                    {
                        attempts++;
                        connectionWatch.Restart();
                    }
                }
                connectionWatch.Stop();

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            try
            {
                // Create a new SpriteBatch, which can be used to draw textures.
                spriteBatch = new SpriteBatch(GraphicsDevice);
                groundTexture = Content.Load<Texture2D>("ground");
                grassTexture = Content.Load<Texture2D>("grass");
                flowerTexture = Content.Load<Texture2D>("flowers");
                stoneTexture = Content.Load<Texture2D>("stone");
                mossyStoneTexture = Content.Load<Texture2D>("mossy-stone");
                sandyPathTexture = Content.Load<Texture2D>("sandy-path");
                gravelPathTexture = Content.Load<Texture2D>("gravel-path");
                waterTexture = Content.Load<Texture2D>("water");
                wavyWaterTexture = Content.Load<Texture2D>("wavy-water");
                bricksTexture = Content.Load<Texture2D>("bricks");
                woodFlooringTexture = Content.Load<Texture2D>("wood-flooring");


                font = Content.Load<SpriteFont>("font1");
                font2 = Content.Load<SpriteFont>("font2");
            }
            catch (Exception e)
            {
                LogException(e);

#if WINDOWS_UAP
                CoreApplication.Exit();
#elif WINDOWS
                Exit();
#endif
            }
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            try
            {
                //Task.Run(async () => await MapService.SaveMapAsync(level)).Wait();
            }
            catch(Exception e)
            {

            }
        }

        protected bool isHoldingTab = false;
        protected DateTime lastUpdate = DateTime.Now;
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            try
            {
                base.Update(gameTime);
            }
            catch(Exception e)
            {

                LogException(e);
#if WINDOWS_UAP
                CoreApplication.Exit();
#elif WINDOWS
                Exit();
#endif
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {

            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            DrawMap();

            foreach(KeyValuePair<Guid, Entity> e in level.gameEntities)
            {
                if (e.Value.ActiveTexture == null) e.Value.LoadContent(Content, GraphicsDevice);
                Vector2 distance = focusedObject.Value.Distance(e.Value);
                e.Value.Draw(spriteBatch, GraphicsDevice, distance);
            }

            foreach(KeyValuePair<Guid, GameObject> o in level.gameObjects)
            {


                if (o.Value.ActiveTexture == null) o.Value.LoadContent(Content, GraphicsDevice);
                if (object.ReferenceEquals(o, focusedObject))
                {
                    o.Value.Draw(spriteBatch, GraphicsDevice, new Vector2(0,0));
                }
                else
                {

                    Vector2 distance = focusedObject.Value.Distance(o.Value);
                    o.Value.Draw(spriteBatch, GraphicsDevice, distance);
                }

            }
            foreach(ButtonObject b in buttons)
            {
                if(b.Background == null) b.LoadContent(Content, GraphicsDevice);
                b.Draw(spriteBatch, GraphicsDevice);
            }

#if DEBUG
            spriteBatch.DrawString(font, "X: " + focusedObject.Value.Position.X, new Vector2(0, 0), Color.Red);
                spriteBatch.DrawString(font, "Y: " + focusedObject.Value.Position.Y, new Vector2(384, 0), Color.Red);
#endif
                spriteBatch.DrawString(font2, WATERMARK, new Vector2(0, GraphicsDevice.PresentationParameters.BackBufferHeight - 48), Color.White);

            base.Draw(gameTime);

            spriteBatch.End();
        }


        //protected Map LoadMap(string mapName)
        //{
        //    return Task.Run(async () =>  await MapService.LoadMapAsync(mapName)).Result;
        //}

        protected void DrawMap()
        {


            ushort YTilePosition = (ushort)(focusedObject.Value.Position.Y / Tile.SIZE_Y);
            ushort XTilePosition = (ushort)(focusedObject.Value.Position.X / Tile.SIZE_X);

            int ymin = YTilePosition - VIEW_DISTANCE;
            int ymax = YTilePosition + VIEW_DISTANCE;
            int xmin = XTilePosition - VIEW_DISTANCE;
            int xmax = XTilePosition + VIEW_DISTANCE;

            if (ymin < 0) ymin = 0;
            if (ymax > Map.SIZE_Y) ymax = (int)Map.SIZE_Y;
            if (xmin < 0) xmin = 0;
            if (xmax > Map.SIZE_X) xmax = (int)Map.SIZE_X;


            for (int y = ymin; y < ymax; y++)
            {
                for (int x = xmin; x < xmax; x++)
                {
                    int positionX = (int)(x * Tile.SIZE_X - focusedObject.Value.Position.X + GraphicsDevice.PresentationParameters.BackBufferWidth / 2 - focusedObject.Value.Size.X / 2);
                    int positionY = (int)(y * Tile.SIZE_Y - focusedObject.Value.Position.Y + GraphicsDevice.PresentationParameters.BackBufferHeight / 2 - focusedObject.Value.Size.Y / 2);

                    if (level.tiles[x, y].typeID == 1)
                    {
                        if (level.tiles[x, y].variantID == 1) spriteBatch.Draw(grassTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                        else if (level.tiles[x, y].variantID == 2) spriteBatch.Draw(flowerTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                        else spriteBatch.Draw(groundTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                    }
                    else if (level.tiles[x, y].typeID == 2)
                    {
                        if (level.tiles[x, y].variantID == 1) spriteBatch.Draw(mossyStoneTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                        else spriteBatch.Draw(stoneTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                    }
                    else if (level.tiles[x, y].typeID == 3)
                    {
                        if (level.tiles[x, y].variantID == 1) spriteBatch.Draw(gravelPathTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                        else spriteBatch.Draw(sandyPathTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                    }
                    else if (level.tiles[x, y].typeID == 4)
                    {
                        if(level.tiles[x, y].variantID == 1) spriteBatch.Draw(wavyWaterTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                        else spriteBatch.Draw(waterTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);

                    }
                    else if (level.tiles[x, y].typeID == 5)
                    {
                        spriteBatch.Draw(bricksTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                    }
                    else if (level.tiles[x, y].typeID == 6)
                    {
                        spriteBatch.Draw(woodFlooringTexture, new Rectangle(positionX, positionY, (int)Tile.SIZE_X, (int)Tile.SIZE_Y), Color.White);
                    }
                }
            }
        }

        private void LogException(Exception e)
        {


#if WINDOWS_UAP

                StorageFile file;
                file = Task.Run(async () => await ApplicationData.Current.LocalFolder.CreateFileAsync(e.GetType().ToString() + ".txt", CreationCollisionOption.OpenIfExists)).Result;

                File.WriteAllText(file.Path, e.StackTrace);


#elif WINDOWS
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\" + e.GetType().ToString() + ".txt", e.StackTrace);
#endif
        }

        static async void ReceiveOutputAsync()
        {

            while (true)
            {
                var obj = await _receiveCeras.ReadFromStream(_netStream);



                if (obj is KeyValuePair<Guid, GameObject>)
                {


                    if (level.gameObjects.TryGetValue(((KeyValuePair<Guid, GameObject>)obj).Key, out GameObject ogo))
                    {
                        level.gameObjects[((KeyValuePair<Guid, GameObject>)obj).Key] = ((KeyValuePair<Guid, GameObject>)obj).Value;
                        focusedObject = (KeyValuePair<Guid, GameObject>)obj;
                        ogo.Dispose();
                    }
                    else
                    {
                        focusedObject = (KeyValuePair<Guid, GameObject>)obj;
                        level.gameObjects.Add(focusedObject.Key, focusedObject.Value);
                    }
                }
                else if (obj is Dictionary<Guid, GameObject>)
                {

                    Dictionary<Guid, GameObject> initialObjects = obj as Dictionary<Guid, GameObject>;


                    Dictionary<Guid, GameObject> copyO = new Dictionary<Guid, GameObject>();
                    foreach (KeyValuePair<Guid, GameObject> kvp in level.gameObjects)
                    {
                        copyO.Add(kvp.Key, kvp.Value);
                    }
                    foreach (KeyValuePair<Guid, GameObject> o in copyO)
                    {
                        if (initialObjects.TryGetValue(o.Key, out GameObject gobj))
                        {
                            level.gameObjects[o.Key].UpdateStats(gobj);
                        }
                        else
                        {
                            level.gameObjects[o.Key].Dispose();
                            level.gameObjects.Remove(o.Key);
                        }
                    }

                    foreach (KeyValuePair<Guid, GameObject> o in initialObjects)
                    {
                        if (!level.gameObjects.TryGetValue(o.Key, out GameObject gobj))
                        {
                            level.gameObjects.Add(o.Key, o.Value);
                        }
                    }
                }
                else if (obj is Dictionary<Guid, Entity>)
                {
                    Dictionary<Guid, Entity> newEntities = obj as Dictionary<Guid, Entity>;


                    Dictionary<Guid, Entity> copyE = new Dictionary<Guid, Entity>();
                    foreach (KeyValuePair<Guid, Entity> kvp in level.gameEntities)
                    {
                        copyE.Add(kvp.Key, (Entity)kvp.Value);
                    }
                    foreach (KeyValuePair<Guid, Entity> e in copyE)
                    {
                        if (newEntities.TryGetValue(e.Key, out Entity ent))
                        {
                            level.gameEntities[e.Key].UpdateStats(ent);
                        }
                        else
                        {
                            level.gameEntities[e.Key].Dispose();
                            level.gameEntities.Remove(e.Key);
                        }
                    }

                    foreach (KeyValuePair<Guid, Entity> e in newEntities)
                    {
                        if (!level.gameEntities.TryGetValue(e.Key, out Entity ent))
                        {
                            level.gameEntities.Add(e.Key, e.Value);
                        }
                    }


                }
                else if(obj is MapColumn[])
                {
                    MapColumn[] colArray = (MapColumn[])obj;

                    for (int i = 0; i < 32; i++)
                    {

                        for (int j = 0; j < Map.SIZE_Y; j++)
                        {
                            level.tiles[colArray[i].columnNumber, j].typeID = colArray[i].typesColumn[j];
                            level.tiles[colArray[i].columnNumber, j].variantID = colArray[i].variantsColumn[j];
                        }
                    }
                }

                _sendCeras.WriteToStream(_netStream, "MapAffrimation");
            }
        }

    }
}
