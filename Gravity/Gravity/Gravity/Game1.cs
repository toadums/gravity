using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using FarseerPhysics.Common;
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using System.Diagnostics;
using WebSocket4Net;
using SimpleJson;
using FarseerPhysics.Dynamics.Contacts;

namespace Gravity
{

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        public static Texture2D Pixi;
        public static Random Game1Random = new Random();

        public static Texture2D backgroundImage = null;
        public static Texture2D planets = null;
        public static  Texture2D gameLogo = null;
        public static Texture2D towers = null;
        public static Texture2D ship = null;
        public static Texture2D menuSprites = null;
        public static Texture2D turnSigns = null;
        public static Texture2D winner = null;
        public static Texture2D loser = null;
        public static Texture2D hearts = null;
        public static Texture2D youWin = null;
        public static Texture2D youLose = null;
        public static Texture2D spaceCorgi = null;


        public static SpriteFont gameFont = null;

        const float unitToPixel = 100.0f;
        const float pixelToUnit = 1 / unitToPixel;

        private World world;

        private bool objectMoving = false;
        private bool objectDrag = false;

        private int windowWidth, windowHeight;
        private float camX, camY;

        private bool freeMove = false;

        private Vector2 originalPos = Vector2.Zero;

        private DeathStar[] DeathStars = new DeathStar[2];
        private bool pinching = false;

        private float lastScale = 1.0f;

        private float zoom = 1.0f;

        WebSocket websocket;

        private List<Projectile> Projectiles = new List<Projectile>();

        private Projectile CurrentProjectile = null;

        private Vector2 LastVelocity;

        private string myTurnPending = "false";
        private bool myTurn = false;

        private bool betweenTurns = true;

        private string playerNum = "";

        private bool colliding = false;

        private int playerHit = 0;
        private int playerHitCity = 0;

        public int DS1Pos = 200;
        public int DS2Pos = 1400;

        public bool isMenuScreen = true;
        public bool isGameScreen = false;
        public bool isEndScreen = false;

        private int myHealth = 6;
        private int oppHealth = 6;
        private const int maxHealth = 6;

        public MainMenu Menu;
        public EndScreen End;
        public Game1()
        {

            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            // Extend battery life under lock.
            InactiveSleepTime = TimeSpan.FromSeconds(1);
            

            Menu = new MainMenu();
            End = new EndScreen();

        }

        void websocket_Opened(object sender, EventArgs e)
        {
            Debug.WriteLine("I CONNECTED");
        }

        void websocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Debug.WriteLine(e);
        }

        void websocket_Closed(object sender, EventArgs e)
        {

        }

        void websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {

            JsonObject msg = SimpleJson.SimpleJson.DeserializeObject<JsonObject>(e.Message);
            string type = (string)msg["type"];
            switch (type)
            {

                case "fire":
                    handleFire(
                        float.Parse(msg["pos_x"].ToString()),
                        float.Parse(msg["pos_y"].ToString()),
                       float.Parse(msg["ang"].ToString()),
                        float.Parse(msg["vel_x"].ToString()),
                        float.Parse(msg["vel_y"].ToString()),
                        float.Parse(msg["inertia"].ToString()),
                        float.Parse(msg["rot"].ToString()));

                    break;
                case "auth":
                    handleAuth((string)msg["mine"], (string)msg["number"]);
                    break;
                case "turn":
                    handleTurn((string)msg["mine"], int.Parse(msg["myHealth"].ToString()), int.Parse(msg["oppHealth"].ToString()));
                    break;
                case "kill":
                    betweenTurns = true;
                    break;
                case "reset":
                    reset();
                    break;
            }
        }

        void handleFire(float posx, float posy, float ang, float velx, float vely, float intertia, float rot)
        {

            if (myTurn || myTurnPending == "true") return;

            CurrentProjectile.Position = new Vector2(posx, posy);
            CurrentProjectile.Proj.AngularVelocity = ang;
            CurrentProjectile.Proj.LinearVelocity = new Vector2(velx, vely);
            CurrentProjectile.Proj.Inertia = intertia;
            CurrentProjectile.Proj.Rotation = rot;

            CurrentProjectile.THISVARIABLEFUCKINGSUCKS = true;
        }

        void handleAuth(string val, string num)
        {
            betweenTurns = false;
            myTurnPending = val;
            playerNum = num;
            colliding = false;

            if (playerNum == "one")
            {
                DeathStars[0] = new DeathStar(new Vector2(100, 100), new Vector2(DS1Pos, 100), new Vector2(1), world, true);
            }
            else
            {
                DeathStars[1] = new DeathStar(new Vector2(100, 100), new Vector2(DS2Pos, 100), new Vector2(1), world, false);
            }
        }

        void handleTurn(string val, int myNewHealth, int oppNewHealth)
        {

            if (myNewHealth <= 0)
            {
                reset();

                End.YouWon = false;
                isGameScreen = false;
                isMenuScreen = false;
                isEndScreen = true;
                return;
            }
            else if(oppNewHealth <= 0)
            {
                reset();

                End.YouWon = true;
                isMenuScreen = false;
                isGameScreen = false;
                isEndScreen = true;
                return;
            }

            Debug.WriteLine("Turn changing");
            myHealth = myNewHealth;
            oppHealth = oppNewHealth;
            betweenTurns = false;
            colliding = false;
            myTurnPending = val;


        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            TouchPanel.EnabledGestures = GestureType.Pinch | GestureType.PinchComplete;
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            Pixi = Content.Load<Texture2D>("pixi");
            backgroundImage = Content.Load<Texture2D>("background");
            planets = Content.Load<Texture2D>("planets");
            gameLogo = Content.Load<Texture2D>("logo");
            towers = Content.Load<Texture2D>("towers");
            ship = Content.Load<Texture2D>("corgiShip");
            menuSprites = Content.Load<Texture2D>("menusprites");
            turnSigns = Content.Load<Texture2D>("turnSigns");
            winner = Content.Load<Texture2D>("pixi");
            loser = Content.Load<Texture2D>("pixi");
            hearts = Content.Load<Texture2D>("hearts");
            youWin = Content.Load<Texture2D>("youWin");
            youLose = Content.Load<Texture2D>("youLose");
            spaceCorgi = Content.Load<Texture2D>("corgi_space");

            gameFont = Content.Load<SpriteFont>("SpriteFont1");

            world = new World(new Vector2(0, 0)); // Grav 0 cause we in space hommie

            Menu.Load(Content);

            DeathStars[0] = new DeathStar(new Vector2(100, 100), new Vector2(DS1Pos, 100), new Vector2(3), world, true);
            DeathStars[1] = new DeathStar(new Vector2(100, 100), new Vector2(DS2Pos, 100), new Vector2(3), world, false);

            //CurrentProjectile = new Projectile(new Vector2(20, 20), new Vector2(400, 250), world, true);

            windowWidth = Window.ClientBounds.Width;
            windowHeight = Window.ClientBounds.Height;

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        private void reset()
        {
            objectMoving = false;
            objectDrag = false;

            Projectiles.Clear();
            myTurn = false;

            zoom = 1.0f;
            lastScale = 1.0f;

            LastVelocity = Vector2.Zero;
            CurrentProjectile = null;
            myTurnPending = null;
            myTurn = true;

            freeMove = false;

            camY = 0;

            camX = 0;
            isGameScreen = false;
            isEndScreen = false;
            isMenuScreen = true;

        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {

            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                if (!isMenuScreen)
                {
                    if(websocket != null && websocket.State == WebSocketState.Open)
                        websocket.Close();
                    isGameScreen = false;
                    isMenuScreen = true;
                }
                else
                {
                    if (websocket != null && websocket.State == WebSocketState.Open)
                        websocket.Close();
                    Exit();
                }
                reset();
            }

            if (isMenuScreen)
            {
                TouchCollection touchCollection = TouchPanel.GetState();

                Menu.Update(touchCollection, gameTime);
                if (Menu.StartPress)
                {



                    isMenuScreen = false;
                    isGameScreen = true;
                    Menu.StartPress = false;

                    websocket = new WebSocket("ws://134.87.140.178:8142/");
                    websocket.EnableAutoSendPing = true;
                    websocket.Opened += websocket_Opened;
                    websocket.Closed += websocket_Closed;
                    websocket.Error += websocket_Error;
                    websocket.MessageReceived += websocket_MessageReceived;
                    websocket.Open();

                }
            }
            else if (isEndScreen)
            {
                TouchCollection touchCollection = TouchPanel.GetState();

                End.Update(touchCollection, gameTime);
            }

            if (betweenTurns)
                return;

            if (myTurnPending == "true")
            {
                objectMoving = false;
                objectDrag = false;

                Projectiles.Clear();

                zoom = 1.0f;
                lastScale = 1.0f;

                LastVelocity = Vector2.Zero;

                myTurnPending = null;
                myTurn = true;

                freeMove = false;


                if (playerNum == "one")
                {

                    if (CurrentProjectile != null && CurrentProjectile.Proj != null)
                    {
                        CurrentProjectile.Proj.OnCollision -= proj_OnCollision;
                    }

                    CurrentProjectile = new Projectile(new Vector2(20, 20), new Vector2(DS1Pos + 100, 250), world, true);
                    CurrentProjectile.Proj.OnCollision += proj_OnCollision;
                }
                else if (playerNum == "two")
                {

                    if (CurrentProjectile != null && CurrentProjectile.Proj != null)
                    {
                        CurrentProjectile.Proj.OnCollision -= proj_OnCollision;
                    }
                    CurrentProjectile = new Projectile(new Vector2(20, 20), new Vector2(DS2Pos - 100, 250), world, true);
                    CurrentProjectile.Proj.OnCollision += proj_OnCollision;
                }

                //THIS FOCUSES ON THE CAMERA WHEN YOU DO A PINCH ZOOM BACK IN, SO WE AREN'T LOST
                camY = (int)(-CurrentProjectile.Position.Y + windowHeight / 2.0f);
                camX = (int)(-CurrentProjectile.Position.X + windowWidth / 2.0f);
            }
            else if (myTurnPending == "false")
            {
                objectMoving = false;
                objectDrag = false;

                Projectiles.Clear();



                zoom = 1.0f;
                lastScale = 1.0f;

                LastVelocity = Vector2.Zero;

                myTurnPending = null;
                myTurn = false;

                freeMove = false;

                if (playerNum == "one")
                {


                    if (CurrentProjectile != null && CurrentProjectile.Proj != null)
                    {
                        CurrentProjectile.Proj.OnCollision -= proj_OnCollision;
                    }
                    CurrentProjectile = new Projectile(new Vector2(20, 20), new Vector2(DS2Pos - 100, 250), world, false);
                    CurrentProjectile.Proj.OnCollision += proj_OnCollision;
                }
                else if (playerNum == "two")
                {


                    if (CurrentProjectile != null && CurrentProjectile.Proj != null)
                    {
                        CurrentProjectile.Proj.OnCollision -= proj_OnCollision;
                    }
                    CurrentProjectile = new Projectile(new Vector2(20, 20), new Vector2(DS1Pos + 100, 250), world, false);
                    CurrentProjectile.Proj.OnCollision += proj_OnCollision;

                }

                //THIS FOCUSES ON THE CAMERA WHEN YOU DO A PINCH ZOOM BACK IN, SO WE AREN'T LOST
                camY = (int)(-CurrentProjectile.Position.Y + windowHeight / 2.0f);
                camX = (int)(-CurrentProjectile.Position.X + windowWidth / 2.0f);
            }

            Vector2 projForce = Vector2.Zero;

            if (TouchPanel.IsGestureAvailable && (myTurn == false || objectMoving)) //THIS IS SO YOU CAN ONLY PINCH ZOOM OUT ON OPPONENT'S TURN
            {
                GestureSample gs = TouchPanel.ReadGesture();
                switch (gs.GestureType)
                {
                    case GestureType.Pinch:

                        break;
                    case GestureType.PinchComplete:

                        if (zoom >= 1)
                        {
                            zoom = 0.25f;
                        }
                        else
                        {
                            zoom = 1.0f;
                            camY = (int)(-CurrentProjectile.Position.Y + windowHeight / 2.0f);
                            camX = (int)(-CurrentProjectile.Position.X + windowWidth / 2.0f);
                        }

                        //blah
                        break;
                }
            }
            else if (!objectMoving && myTurn == true) //THIS MAKES IT SO YOU CAN'T SHOOT THE PROJ ON YOUR OPPONENTS TURN
            {
                TouchCollection touchCollection = TouchPanel.GetState();
                foreach (TouchLocation tl in touchCollection)
                {
                    if ((tl.State == TouchLocationState.Pressed) || (tl.State == TouchLocationState.Moved))
                    {

                        TouchLocation old;

                        tl.TryGetPreviousLocation(out old);
                        if (CurrentProjectile != null && Vector2.Distance(tl.Position - new Vector2(camX, camY), CurrentProjectile.Position) < 50)
                        {

                            if (old.Position.X > 0)
                                CurrentProjectile.Position = (CurrentProjectile.Position + (tl.Position - old.Position));
                            else
                                originalPos = tl.Position;
                            objectDrag = true;
                        }
                        else
                        {

                            if (old.Position.X > 0)
                            {
                                camX = camX + (tl.Position.X - old.Position.X);
                                camY = camY + (tl.Position.Y - old.Position.Y);
                            }
                            else
                                originalPos = tl.Position;

                        }


                    }
                    else if (tl.State == TouchLocationState.Released && objectDrag)
                    {
                        Debug.WriteLine("Shit dawg");
                        objectMoving = true;
                        objectDrag = false;

                        projForce = originalPos - tl.Position;
                        Vector2 oldForce = projForce;

                        float len = projForce.Length() * 15;
                        projForce.Normalize();

                        projForce = projForce * len * pixelToUnit;

                        camX = CurrentProjectile.Position.X;
                        camY = CurrentProjectile.Position.Y;

                        JsonObject msg = new JsonObject();
                        msg["type"] = "initialFire";

                        websocket.Send(SimpleJson.SimpleJson.SerializeObject(msg));

                    }
                }
            }
            else
            {
                TouchCollection touchCollection = TouchPanel.GetState();
                foreach (TouchLocation tl in touchCollection)
                {
                    if ((tl.State == TouchLocationState.Pressed) || (tl.State == TouchLocationState.Moved))
                    {

                        TouchLocation old;

                        tl.TryGetPreviousLocation(out old);
                        if (Vector2.Distance(tl.Position - new Vector2(camX, camY), CurrentProjectile.Position) > 50)
                        {
                            if (old.Position.X > 0)
                            {
                                camX = camX + (tl.Position.X - old.Position.X);
                                camY = camY + (tl.Position.Y - old.Position.Y);
                            }
                            else
                                originalPos = tl.Position;
                            freeMove = true;
                        }
                    }
                }
            }

            if (CurrentProjectile != null && !CurrentProjectile.Mine && CurrentProjectile.THISVARIABLEFUCKINGSUCKS)
            {
                objectMoving = true;
                objectDrag = false;

                projForce = LastVelocity;

                float len = projForce.Length() * 15;
                projForce.Normalize();

                projForce = projForce * len * pixelToUnit;

                camY = (int)(-CurrentProjectile.Position.Y + windowHeight / 2.0f);
                camX = (int)(-CurrentProjectile.Position.X + windowWidth / 2.0f);

                CurrentProjectile.THISVARIABLEFUCKINGSUCKS = false;
            }

            if (objectMoving)
            {


                foreach (DeathStar d in DeathStars)
                {
                    Vector2 forcedir = (d.Position) - CurrentProjectile.Position;
                    float len = forcedir.Length();
                    forcedir.Normalize();

                    if (len < 1000)
                    {
                        forcedir = forcedir * (1 - len / 1000) * d.Pull;
                        projForce += forcedir;

                    }

                }

                if (CurrentProjectile.Mine)
                {
                    try
                    {
                        JsonObject msg = new JsonObject();
                        msg["type"] = "fire";
                        msg["pos_x"] = CurrentProjectile.Position.X;
                        msg["pos_y"] = CurrentProjectile.Position.Y;
                        msg["ang"] = CurrentProjectile.Proj.AngularVelocity;
                        msg["vel_x"] = CurrentProjectile.Proj.LinearVelocity.X;
                        msg["vel_y"] = CurrentProjectile.Proj.LinearVelocity.Y;
                        msg["inertia"] = CurrentProjectile.Proj.Inertia;
                        msg["rot"] = CurrentProjectile.Proj.Rotation;

                        websocket.Send(SimpleJson.SimpleJson.SerializeObject(msg));
                    }
                    catch (Exception e)
                    {

                    }

                    CurrentProjectile.Proj.ApplyForce(projForce);
                }
                else
                {

                }
            }

            if (!(colliding == false && CurrentProjectile != null))
            {
                colliding = false;
                
                Debug.WriteLine("GG PLANET, YOU HIT");
                betweenTurns = true;
                try
                {
                    if (CurrentProjectile != null && CurrentProjectile.Proj != null)
                    {
                        CurrentProjectile.Proj.OnCollision -= proj_OnCollision;
                    }
                    JsonObject msg = new JsonObject();
                    msg["type"] = "turn";
                    if (playerHit == 1)
                        msg["hit"] = "one";
                    else if (playerHit == 2)
                        msg["hit"] = "two";
                    else if (playerHitCity == 1)
                        msg["cityHit"] = "one";
                    else if (playerHitCity == 2)
                        msg["cityHit"] = "two";
                    else
                    {
                        msg["hit"] = "zero";
                        msg["cityHit"] = "zero";
                    }

                    playerHit = 0;
                    playerHitCity = 0;

                    if (myTurn)
                    {
                        websocket.Send(SimpleJson.SimpleJson.SerializeObject(msg));
                    }
                    CurrentProjectile.Proj.OnCollision += proj_OnCollision;
                }
                catch (Exception e)
                {

                }

            }

            world.Step((float)0.03f);

            if (objectMoving && !freeMove)
            {

                camY = (int)(-CurrentProjectile.Position.Y + windowHeight / 2.0f);

                camX = (int)(-CurrentProjectile.Position.X + windowWidth / 2.0f);

            }

            base.Update(gameTime);
        }

        int i = 0;

        private bool proj_OnCollision(Fixture f1, Fixture f2, Contact contact)
        {
            /*if ((f1.Body == CurrentProjectile.Proj && f2.Body == DeathStars[0].Planet && colliding == false))
            {
                colliding = true;
                playerHit = 1;
            }
            else if (f1.Body == CurrentProjectile.Proj && f2.Body == DeathStars[1].Planet && colliding == false)
            {
                colliding = true;
                playerHit = 2;
            }*/
            if (betweenTurns || colliding || !(myTurn)  || myTurnPending != null)
            {
                return false;
            }
            if ((f1.Body == CurrentProjectile.Proj) && colliding == false)
            {
                if (f2.Body == DeathStars[0].Planet)
                {
                    colliding = true;
                    playerHit = 1;
                }
                else if(f2.Body == DeathStars[1].Planet)
                {
                    colliding = true;
                    playerHit = 2;
                }
                else if (f2.Body == DeathStars[0].City)
                {
                    colliding = true;
                    playerHitCity = 1;
                }
                else if (f2.Body == DeathStars[1].City)
                {
                    colliding = true;
                    playerHitCity = 2;
                }
            }

            return true;
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Vector3 transVector = new Vector3(camX, camY, 0.0f);

            if (isMenuScreen)
            {
                Menu.render(spriteBatch);
            }
            else if (isEndScreen)
            {
                End.render(spriteBatch);
            }
            else if (isGameScreen)
            {

                //GAME WORLD
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, null, Matrix.CreateTranslation(transVector) * Matrix.CreateScale(new Vector3(zoom, zoom, 1)));
                spriteBatch.Draw(backgroundImage, new Rectangle(-4000, -3500, 8000, 7000), Color.White);

                if (CurrentProjectile != null)
                {
                    //spriteBatch.Draw(Game1.Pixi, CurrentProjectile.Position, null, Color.Yellow, CurrentProjectile.Proj.Rotation, new Vector2(Game1.Pixi.Width / 2.0f, Game1.Pixi.Height / 2.0f), CurrentProjectile.Size, SpriteEffects.None, 0);
                    spriteBatch.Draw(Game1.ship, CurrentProjectile.Position, null, Color.White, (float)Math.PI + (float)Math.Atan2(CurrentProjectile.Proj.LinearVelocity.Y, CurrentProjectile.Proj.LinearVelocity.X), new Vector2(Game1.ship.Width / 2.0f, Game1.ship.Height / 2.0f), 0.2f, SpriteEffects.None, 0.0f);
                }

                foreach (DeathStar d in DeathStars)
                {
                    //spriteBatch.Draw(Game1.Pixi, d.Position, null, Color.Red, d.Planet.Rotation, new Vector2(Game1.Pixi.Width / 2.0f, Game1.Pixi.Height / 2.0f), d.Size, SpriteEffects.None, 0);
                    spriteBatch.Draw(planets, d.Position - new Vector2(50), new Rectangle(100 * d.index, 0, 100, 100), Color.White);
                    //spriteBatch.Draw(Game1.Pixi, d.CityPosition, null, Color.Green, d.City.Rotation, new Vector2(Game1.Pixi.Width / 2.0f, Game1.Pixi.Height / 2.0f), d.CitySize, SpriteEffects.None, 0);
                    spriteBatch.Draw(towers, d.CityPosition + (d.left ? Vector2.Zero : new Vector2(23, 0)), new Rectangle(0, (d.left) ? 44 : 0, 100, 44), Color.White, 0.0f, new Vector2(towers.Width / 2.0f + 10, towers.Height / 4.0f), 1.0f, d.left ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
                }

                spriteBatch.End();

                //GUI
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                spriteBatch.Draw(turnSigns, new Vector2(turnSigns.Width + spriteBatch.GraphicsDevice.DisplayMode.Width + 100, 20) / 4, new Rectangle(0, myTurn ? 0 : 60, 324, 60), Color.White);

                for (int i = 0; i < maxHealth; i++)
                {
                    spriteBatch.Draw(hearts, new Rectangle(10 + (32 * i), 10, 32, 32), new Rectangle((i < myHealth ? 0 : 128), 0, 128, 128), Color.White);
                    spriteBatch.Draw(hearts, new Rectangle((spriteBatch.GraphicsDevice.DisplayMode.Width / 2) + (turnSigns.Width) + 10 + (32 * i), 10, 32, 32), new Rectangle((i < oppHealth ? 0 : 128), 0, 128, 128), Color.White);
                }

                spriteBatch.End();
            }
        
            base.Draw(gameTime);
        }
    }
}
