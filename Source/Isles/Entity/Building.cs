//-----------------------------------------------------------------------------
//  Isles v1.0
//  
//  Copyright 2008 (c) Nightin Games. All Rights Reserved.
//-----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Isles.Engine;
using Isles.Graphics;

namespace Isles
{
    #region Building
    /// <summary>
    /// Respresents a building in the game
    /// </summary>
    public class Building : Entity
    {
        #region Building State & Flag
        /// <summary>
        /// State of the building
        /// </summary>
        public enum BuildingState
        {
            /// <summary>
            /// Normal state
            /// </summary>
            Normal,

            /// <summary>
            /// The user is finding where to place the building
            /// </summary>
            Locating,

            /// <summary>
            /// The building is under construction
            /// </summary>
            Constructing,

            /// <summary>
            /// The construction stops due to lack of resource
            /// </summary>
            Onhold,

            /// <summary>
            /// The building is destroyed, leaving some ashes
            /// </summary>
            Destroyed,
        }

        /// <summary>
        /// Flag for a building
        /// </summary>
        [Flags()]
        public enum BuildingFlag
        {
            StoresWood = 1 << 0,
            StoresGold = 1 << 1,
            StoresFood = 1 << 2,
        }
        #endregion

        #region Fields
        /// <summary>
        /// Whether the location is valid for this building
        /// </summary>
        protected bool isValidLocation = true;

        /// <summary>
        /// Gets or sets max health of the building
        /// </summary>
        public float MaxHealth
        {
            get { return maxHealth; }

            set
            {
                maxHealth = (value > 0 ? value : 0);
                if (health > 0)
                    health = maxHealth;
            }
        }

        protected float maxHealth = 100;

        /// <summary>
        /// Gets or sets the health of the building
        /// </summary>
        public float Health
        {
            get { return health; }

            set
            {
                if (value <= 0)
                {
                    health = 0;
                    state = BuildingState.Destroyed;
                    // TODO: destroy the building
                }
                else
                {
                    health = value;
                }
            }
        }

        protected float health = 100;


        /// <summary>
        /// Gets the building state
        /// </summary>
        public BuildingState State
        {
            get { return state; }
        }

        protected BuildingState state = BuildingState.Locating;


        /// <summary>
        /// Gets or set the base height of the building
        /// </summary>
        public float BaseHeight
        {
            get { return baseHeight; }
            set { baseHeight = value; }
        }

        protected float baseHeight = 0;


        /// <summary>
        /// Gets or sets the time to construct this building
        /// </summary>
        public float ConstructionTime;


        /// <summary>
        /// Gets or sets how much wood needed for the building
        /// </summary>
        public int Wood;


        /// <summary>
        /// Gets or sets how much gold needed for the building
        /// </summary>
        public int Gold;


        /// <summary>
        /// Gets or sets building flag
        /// </summary>
        public BuildingFlag Flag;

        public bool StoresWood
        {
            get { return (Flag & BuildingFlag.StoresWood) == BuildingFlag.StoresWood; }
        }

        public bool StoresGold
        {
            get { return (Flag & BuildingFlag.StoresGold) == BuildingFlag.StoresGold; }
        }

        public bool StoresFood
        {
            get { return (Flag & BuildingFlag.StoresFood) == BuildingFlag.StoresFood; }
        }
        #endregion
        
        #region Methods
        /// <summary>
        /// Create a new building
        /// </summary>
        public Building(GameWorld world, string classID) : base(world)
        {
            XmlElement xml;

            if (GameDefault.Singleton.WorldObjectDefaults.TryGetValue(classID, out xml))
            {
                Deserialize(xml);
            }
        }

        public override void Deserialize(XmlElement xml)
        {
            bool result;

            if (bool.TryParse(xml.GetAttribute("StoresWood"), out result) && result)
                Flag |= BuildingFlag.StoresWood;

            if (bool.TryParse(xml.GetAttribute("StoresGold"), out result) && result)
                Flag |= BuildingFlag.StoresGold;

            if (bool.TryParse(xml.GetAttribute("StoresFood"), out result) && result)
                Flag |= BuildingFlag.StoresFood;

            int.TryParse(xml.GetAttribute("Wood"), out Wood);
            int.TryParse(xml.GetAttribute("Gold"), out Gold);

            float.TryParse(xml.GetAttribute("Health"), out health);
            float.TryParse(xml.GetAttribute("MaxHealth"), out maxHealth);
            float.TryParse(xml.GetAttribute("BaseHeight"), out baseHeight);
            float.TryParse(xml.GetAttribute("ConstructionTime"), out ConstructionTime);

            if (health > maxHealth)
            {
                if (maxHealth >= 0)
                    health = maxHealth;
                else
                    maxHealth = health;
            }

            // Deserialize models after default attributes are assigned
            base.Deserialize(xml);
        }

        /// <summary>
        /// Tests to see if the building is in a valid location
        /// </summary>
        /// <remarks>
        /// You can only rotate the building around Z axis to let this
        /// algorithm work !!!
        /// </remarks>
        /// <returns></returns>
        public bool IsValidLocation(Landscape landscape)
        {
            // Make sure it's on the landscape
            if (Position.X < 0 || Position.Y < 0 ||
                Position.X > landscape.Size.X || Position.Y > landscape.Size.Y)
            {
                return false;
            }

            // Keep track of entities
            foreach (IWorldObject o in world.GetNearbyObjects(BoundingBox))
            {
                Entity e = o as Entity;

                if (e != this && e != null &&
                    Outline.Intersects(Outline, e.Outline) != ContainmentType.Disjoint)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Test if a grid is valid for this building
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        bool IsValidGrid(Landscape landscape, Point grid, float z)
        {
            // Should be on the ground and not owned by others
            //if (landscape.Data[grid.X, grid.Y].Type != LandscapeType.Ground/* ||
            //    landscape.Data[grid.X, grid.Y].Owners.Count != 0*/)
            //    return false;

            // Shouldn't be on the water
            if (landscape.HeightField[grid.X, grid.Y] < 0)
                return false;

            // Shouldn't be on somewhere too rough
            const float Cos30 = 0.8660254037f;
            if (Vector3.Dot(landscape.NormalField[grid.X, grid.Y], Vector3.UnitZ) < Cos30)
                return false;
            
            return true;
        }

        /// <summary>
        /// Place the game entity somewhere on the ground and complete it immediately
        /// </summary>
        /// <returns>Success or not</returns>
        //public bool PlaceAndCompleteBuild(Landscape landscape, Vector3 newPosition, float newRotation)
        //{
        //    if (!Place(landscape, newPosition, newRotation))
        //        return false;

        //    CompleteBuild();
        //    return true;
        //}

        /// <summary>
        /// Place the game entity somewhere on the ground
        /// </summary>
        /// <returns>Success or not</returns>
        //public override bool Place(Landscape landscape, Vector3 newPosition, float newRotation)
        //{
        //    // Store new Position/Rotation
        //    Position = newPosition;
        //    Rotation = newRotation;

        //    if (!IsValidLocation(landscape))
        //        return false;

        //    // Fall on the ground
        //    Position = new Vector3(
        //        Position.X, Position.Y, landscape.GetHeight(Position.X, Position.Y));

        //    // Change grid owner
        //    foreach (Point grid in landscape.EnumerateGrid(Position, Size))
        //        landscape.Data[grid.X, grid.Y].Owners.Add(this);

        //    // Set state to constructing
        //    state = BuildingState.Constructing;
        //    startTime = BaseGame.Singleton.CurrentGameTime.TotalGameTime.TotalSeconds;

        //    return true;
        //}
        #endregion

        #region Drag & Drop
        public override void EndDrag(Hand hand)
        {
            // Nothing will be dragged, since we can't change
            // a building's Position once placed :)
        }

        public override void Follow(Hand hand)
        {
            isValidLocation = IsValidLocation(world.Landscape);
            base.Follow(hand);
        }

        public override bool BeginDrop(Hand hand, Entity entity, bool leftButton)
        {
            // Right click will cancel building
            if (!leftButton)
            {
                hand.Drop();
                world.Destroy(this);
                return false;
            }

            return base.BeginDrop(hand, entity, leftButton);
        }

        public override void Dropping(Hand hand, Entity entity, bool leftButton)
        {
            isValidLocation = IsValidLocation(world.Landscape);
            base.Dropping(hand, entity, leftButton);
        }

        #endregion

        #region Update & Draw

        /// <summary>
        /// Progress when constructing the building.
        /// </summary>
        double startTime;
        int woodSpend, goldSpend;
        float progress;

        /// <summary>
        /// Update
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            if (state == BuildingState.Constructing)
            {
                if (ConstructionTime > 0)
                {
                    progress = (float)(
                        (gameTime.TotalGameTime.TotalSeconds - startTime) / ConstructionTime);
                }
                else
                {
                    progress = 1;
                }

                // Check to see if we have enough wood and gold
                int wood = (int)(Wood * progress - woodSpend);
                int gold = (int)(Gold * progress - goldSpend);

                if (wood <= world.GameLogic.Wood &&
                    gold <= world.GameLogic.Gold)
                {
                    if (woodSpend + wood >= Wood &&
                        goldSpend + gold >= Gold)
                    {
                        wood = Wood - woodSpend;
                        gold = Gold - goldSpend;

                        CompleteBuild();
                    }

                    world.GameLogic.Wood -= wood;
                    world.GameLogic.Gold -= gold;

                    woodSpend += wood;
                    goldSpend += gold;
                }
                else
                {
                    // Otherwise our building is on hold
                    state = BuildingState.Onhold;
                }
            }
            else if (state == BuildingState.Onhold)
            {
                startTime += gameTime.ElapsedGameTime.TotalSeconds;

                // Check to see if we have additional wood and gold
                float f = (float)(
                    (gameTime.TotalGameTime.TotalSeconds - startTime) / ConstructionTime);

                // Check to see if we have enough wood and gold
                int wood = (int)(Wood * f - woodSpend);
                int gold = (int)(Gold * f - goldSpend);

                if (wood <= world.GameLogic.Wood && gold <= world.GameLogic.Gold)
                {
                    state = BuildingState.Constructing;
                }
            }

            base.Update(gameTime);
        }

        private void CompleteBuild()
        {
            // Construction conplete
            state = BuildingState.Normal;

            // Building finished, update dependencies
            world.GameLogic.Dependencies[Name] = true;
        }

        public override Rectangle DrawStatus(Rectangle region)
        {
            Text.DrawString("Name: " + Name + "\nDescription: " + Description,
                15, new Vector2((float)region.X, (float)region.Y), Color.Orange);

            return region;
        }
        #endregion
    }

    #endregion
}
