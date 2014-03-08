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

        const float unitToPixel = 100.0f;
        const float pixelToUnit = 1 / unitToPixel;

        private World world;

        private Body Planet;
        private Vector2 PlanetSize = new Vector2(100 * pixelToUnit, 100 * pixelToUnit);

        private Vector2 PlanetGrav = new Vector2(10 * pixelToUnit, 10 * pixelToUnit);

        private Body Projectile;
        private Vector2 ProjectileSize = new Vector2(20 * pixelToUnit, 20 * pixelToUnit);

        private bool objectMoving = false;
        private bool objectDrag = false;

        private int windowWidth, windowHeight;
        private float camX, camY;

        private bool freeMove = false;

        private Vector2 originalPos = Vector2.Zero;

        private bool pinching = false;

        private float lastScale = 1.0f;

        private float zoom = 1.0f;

        public Game1()
        {

            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            // Extend battery life under lock.
            InactiveSleepTime = TimeSpan.FromSeconds(1);
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

            world = new World(new Vector2(0, 0)); // Grav 0 cause we in space hommie

            Planet = BodyFactory.CreateRectangle(world, 100 * pixelToUnit, 100 * pixelToUnit, 10f);
            Planet.BodyType = BodyType.Static;
            Planet.Position = new Vector2(400 * pixelToUnit, 220 * pixelToUnit);

            Projectile = BodyFactory.CreateRectangle(world, 20 * pixelToUnit, 20 * pixelToUnit, 10f);
            Projectile.BodyType = BodyType.Dynamic;
            Projectile.Position = originalPos = new Vector2(200 * pixelToUnit, 50 * pixelToUnit);
            Projectile.ApplyForce(new Vector2(20, 20));

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
                objectMoving = false;
                objectDrag = false;

                Projectile.Position = new Vector2(200 * pixelToUnit, 50 * pixelToUnit);
                Projectile.AngularVelocity = 0;
                Projectile.LinearVelocity = Vector2.Zero;
                Projectile.ApplyForce(Vector2.Zero);
                Projectile.ApplyAngularImpulse(0);
                Projectile.Rotation = 0;

                camY = 0;

                camX = 0;

                zoom = 1.0f;
                lastScale = 1.0f;

                freeMove = false;
            }

            Vector2 projForce = Vector2.Zero;

            if (TouchPanel.IsGestureAvailable)
            {
                GestureSample gs = TouchPanel.ReadGesture();
                switch (gs.GestureType)
                {
                    case GestureType.Pinch:
                        
                        break;
                    case GestureType.PinchComplete:

                        if (zoom >= 1)
                        {
                            zoom = 0.5f;
                        }
                        else
                        {
                            zoom = 1.0f;
                        }

                        //blah
                        break;
                }
            }
            else if (!objectMoving)
            {
                TouchCollection touchCollection = TouchPanel.GetState();
                foreach (TouchLocation tl in touchCollection)
                {
                    if ((tl.State == TouchLocationState.Pressed) || (tl.State == TouchLocationState.Moved))
                    {

                        TouchLocation old;

                        tl.TryGetPreviousLocation(out old);
                        Debug.WriteLine(Vector2.Distance(tl.Position - new Vector2(camX, camY), Projectile.Position * unitToPixel));
                        if (Vector2.Distance(tl.Position - new Vector2(camX, camY), Projectile.Position * unitToPixel) < 100)
                        {

                            if (old.Position.X > 0)
                                Projectile.Position = (Projectile.Position * unitToPixel + (tl.Position - old.Position)) * pixelToUnit;
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
                        objectMoving = true;
                        objectDrag = false;

                        projForce = originalPos - tl.Position;
                        float len = projForce.Length() * 4;
                        projForce.Normalize();

                        projForce = projForce * len * pixelToUnit;

                        camX = Projectile.Position.X * unitToPixel;
                        camY = Projectile.Position.Y * unitToPixel;

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
                        Debug.WriteLine(Vector2.Distance(tl.Position - new Vector2(camX, camY), Projectile.Position * unitToPixel));
                        if (Vector2.Distance(tl.Position - new Vector2(camX, camY), Projectile.Position * unitToPixel) > 100)
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

            if (objectMoving)
            {

                Vector2 forcedir = (Planet.Position + PlanetSize / 2) * unitToPixel - (Projectile.Position + ProjectileSize / 2) * unitToPixel;
                float len = forcedir.Length();
                forcedir.Normalize();

                forcedir = forcedir * 0.5f;

                projForce += forcedir;


                Projectile.ApplyForce(projForce);
            }

            world.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

            if (objectMoving && !freeMove)
            {
                    
                    camY = (int)(-Projectile.Position.Y * unitToPixel + windowHeight / 2.0f);

                    camX = (int)(-Projectile.Position.X * unitToPixel + windowWidth / 2.0f);
                
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Vector3 transVector = new Vector3(camX, camY, 0.0f);

            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, null, null, null, null, Matrix.CreateTranslation(transVector) * Matrix.CreateScale(new Vector3(zoom, zoom, 1)));

            //spriteBatch.Begin();

            spriteBatch.Draw(Game1.Pixi, Planet.Position * unitToPixel, null, Color.Red, Planet.Rotation, new Vector2(Game1.Pixi.Width / 2.0f, Game1.Pixi.Height / 2.0f), new Vector2(PlanetSize.X * unitToPixel, PlanetSize.Y * unitToPixel), SpriteEffects.None, 0);
            spriteBatch.Draw(Game1.Pixi, Projectile.Position * unitToPixel, null, Color.Yellow, Projectile.Rotation, new Vector2(Game1.Pixi.Width / 2.0f, Game1.Pixi.Height / 2.0f), new Vector2(ProjectileSize.X * unitToPixel, ProjectileSize.Y * unitToPixel), SpriteEffects.None, 0);

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
