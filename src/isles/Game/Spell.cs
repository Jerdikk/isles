// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Isles.Engine;
using Isles.Graphics;
using Isles.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Isles
{
    /// <summary>
    /// A rectangle on a texture.
    /// Currently all icons are placed in the same texture for simplicity.
    /// </summary>
    public struct Icon
    {
        /// <summary>
        /// Gets or sets the rectangle region on the texture.
        /// </summary>
        public Rectangle Region;

        /// <summary>
        /// Gets or sets the icon texture.
        /// </summary>
        public Texture2D Texture;

        /// <summary>
        /// For easy creation of icons.
        /// </summary>
        public Icon(Texture2D texture)
        {
            Texture = texture;
            Region = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        public Icon(Texture2D texture, Rectangle region)
        {
            Texture = texture;
            Region = region;
        }

        /// <summary>
        /// Static stuff.
        /// </summary>
        public static Texture2D DefaultTexture
        {
            get
            {
                if (texture == null || texture.IsDisposed)
                {
                    texture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/Icons.png");
                }

                return texture;
            }
        }

        private static Texture2D texture;
        private const int XCount = 8;
        private const int YCount = 8;

        public static Icon FromTiledTexture(int n)
        {
            return FromTiledTexture(n, XCount, YCount, DefaultTexture);
        }

        public static int IndexFromRectangle(Rectangle rectangle)
        {
            return rectangle.Y * YCount / DefaultTexture.Height * XCount +
                   rectangle.X * XCount / DefaultTexture.Width;
        }

        public static Rectangle RectangeFromIndex(int n)
        {
            var x = n % XCount;
            var y = n / XCount;
            var w = DefaultTexture.Width / XCount;
            var h = DefaultTexture.Height / YCount;
            return new Rectangle(x * w, y * h, w, h);
        }

        public static Icon FromTiledTexture(
            int n, int xCount, int yCount, Texture2D texture)
        {
            var x = n % xCount;
            var y = n / xCount;
            var w = texture.Width / xCount;
            var h = texture.Height / yCount;
            return new Icon(texture, new Rectangle(x * w, y * h, w, h));
        }
    }

    /// <summary>
    /// Base class for all game spells.
    /// A new spell instance is created whenever a spell is been casted.
    /// </summary>
    public abstract class Spell : IEventListener
    {
        /// <summary>
        /// Spell creators.
        /// </summary>
        private static readonly Dictionary<string, Func<GameWorld, Spell>> creators = new();

        public static void RegisterCreator(Type spellType, Func<GameWorld, Spell> creator)
        {
            creators.Add(spellType.Name, creator);
        }

        public static void RegisterCreator(string spellName, Func<GameWorld, Spell> creator)
        {
            creators.Add(spellName, creator);
        }

        public static Spell Create(Type spellType, GameWorld world)
        {
            return Create(spellType.Name, world);
        }

        /// <summary>
        /// Create a new game spell.
        /// </summary>
        /// <param name="spellTypeName"></param>
        /// <param name="world"></param>
        public static Spell Create(string spellTypeName, GameWorld world)
        {
            return !creators.TryGetValue(spellTypeName, out var creator)
                ? throw new Exception("Failed to create spell, unknown spell type: " + spellTypeName)
                : creator(world);
        }

        /// <summary>
        /// Cast a spell.
        /// </summary>
        public static void Cast(Spell spell)
        {
            if (CurrentSpell == null && spell.Trigger())
            {
                CurrentSpell = spell;
            }
        }

        /// <summary>
        /// Stops casting the spell.
        /// </summary>
        public static void EndSpell()
        {
            CurrentSpell = null;
        }

        /// <summary>
        /// Gets the active spell.
        /// </summary>
        public static Spell CurrentSpell { get; private set; }

        protected GameWorld world;

        public GameObject Owner
        {
            get => owner;
            set
            {
                owner = value;
                OnOwnerChanged();
            }
        }

        protected virtual void OnOwnerChanged() { }

        private GameObject owner;

        public bool Enable
        {
            get => enable;

            set
            {
                enable = value;
                if (spellButton != null)
                {
                    spellButton.Visible = value;
                }
            }
        }

        private bool enable = true;

        public Spell(GameWorld world)
        {
            this.world = world;
        }

        public Spell(GameWorld world, string classID)
        {
            this.world = world;

            // Initialize from xml element
            if (GameDefault.Singleton.WorldObjectDefaults.TryGetValue(classID, out XmlElement xml))
            {
                Deserialize(xml);
            }

            if (GameDefault.Singleton.SpellDefaults.TryGetValue(classID, out xml))
            {
                Deserialize(xml);
            }
        }

        public virtual void Deserialize(XmlElement xml)
        {
            Name = xml.GetAttribute("Name");
            Description = xml.GetAttribute("Description");
            Description.Replace('\t', ' ');

            string value;

            if ((value = xml.GetAttribute("Icon")) != "")
            {
                Icon = Icon.FromTiledTexture(int.Parse(value));
            }

            if ((value = xml.GetAttribute("Hotkey")) != "")
            {
                Hotkey = (Keys)Enum.Parse(typeof(Keys), value);
            }

            if ((value = xml.GetAttribute("CoolDown")) != "")
            {
                CoolDown = float.Parse(value);
            }

            if ((value = xml.GetAttribute("AutoCast")) != "")
            {
                AutoCast = bool.Parse(value);
            }
        }

        /// <summary>
        /// Gets or sets the name of the spell.
        /// </summary>
        public string Name;

        /// <summary>
        /// Gets or sets the description of the spell.
        /// </summary>
        public string Description;

        /// <summary>
        /// Gets or sets the spell icon.
        /// </summary>
        public Icon Icon = Icon.FromTiledTexture(0);

        /// <summary>
        /// Gets or sets the hot key for the spell.
        /// </summary>
        public Keys Hotkey;

        /// <summary>
        /// Gets whether the spell is ready for cast.
        /// </summary>
        public virtual bool Ready => CoolDownElapsed >= CoolDown;

        /// <summary>
        /// Gets the cool down time after the spell has been casted.
        /// </summary>
        public virtual float CoolDown
        {
            get => coolDown;

            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                coolDown = value;
                coolDownElapsed = coolDown;
            }
        }

        private float coolDown;

        /// <summary>
        /// Gets how many seconds elapsed after the spell has been casted.
        /// </summary>
        public virtual float CoolDownElapsed
        {
            get => coolDownElapsed;
            set => coolDownElapsed = value;
        }

        private float coolDownElapsed;

        /// <summary>
        /// Gets or sets whether auto cast is enable for this spell.
        /// </summary>
        public virtual bool AutoCast
        {
            get => autoCast;
            set => autoCast = value;
        }

        private bool autoCast;

        /// <summary>
        /// Gets the button for this spell.
        /// </summary>
        public virtual IUIElement Button
        {
            get
            {
                if (spellButton == null || spellButton.Texture == null ||
                    spellButton.Texture.IsDisposed)
                {
                    spellButton = CreateUIElement(GameUI.Singleton);
                    spellButton.Visible = Enable;
                }

                return spellButton;
            }
        }

        protected SpellButton spellButton;
        private TipBox spellTip;

        /// <summary>
        /// Creates an UIElement for the spell.
        /// </summary>
        protected virtual SpellButton CreateUIElement(GameUI ui)
        {
            if (ui == null)
            {
                throw new ArgumentNullException();
            }

            var baseIcon = Icon.IndexFromRectangle(Icon.Region);
            spellButton = new SpellButton
            {
                Texture = Icon.Texture == null ? Icon.DefaultTexture : Icon.Texture,
                SourceRectangle = Icon.Region,
                Pressed = Icon.RectangeFromIndex(baseIcon + 1),
                Hovered = Icon.RectangeFromIndex(baseIcon + 2),
                HotKey = Hotkey,
                Tag = this,
            };

            spellButton.Click += (sender, e) =>
            {
                if ((sender as Button).Tag is not Spell spell)
                {
                    throw new InvalidOperationException();
                }

                // Cast the spell
                Spell.Cast(spell);
            };

            spellButton.Enter += (sender, e) =>
            {
                GameDefault gameDefault = GameDefault.Singleton;

                spellTip = CreateTipBox();

                GameUI.Singleton.TipBoxContainer.Add(spellTip);
            };

            spellButton.Leave += (sender, e) =>
            {
                if (spellTip != null)
                {
                    GameUI.Singleton.TipBoxContainer.Remove(spellTip);
                }
            };

            return spellButton;
        }

        protected virtual TipBox CreateTipBox()
        {
            TextField content = null;
            var title = new TextField(Name, 16f / 23, Color.Gold,
                                             new Rectangle(6, 6, 210, 30));
            if (Description != null && Description != "")
            {
                content = new TextField(Description, 15f / 23, Color.White,
                                        new Rectangle(6, 30, 210, 100));
            }

            var spellTip = new TipBox(222, title.RealHeight +
                                        (content != null ? content.RealHeight : 0) + 20);
            spellTip.Add(title);
            if (content != null)
            {
                spellTip.Add(content);
            }

            return spellTip;
        }

        public virtual void Update(GameTime gameTime)
        {
            // Update spell cooldown
            if (coolDown > 0)
            {
                coolDownElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (coolDownElapsed > coolDown)
                {
                    coolDownElapsed = coolDown;
                }

                if (spellButton != null)
                {
                    spellButton.Percentage = 100 * coolDownElapsed / coolDown;
                }
            }
        }

        /// <summary>
        /// Called every frame when this spell have become the active spell.
        /// </summary>
        /// <param name="gameTime"></param>
        public virtual void UpdateCast(GameTime gameTime) { }

        /// <summary>
        /// Draw the spell.
        /// </summary>
        /// <param name="gameTime"></param>
        public virtual void Draw(GameTime gameTime) { }

        /// <summary>
        /// IEventListener.
        /// </summary>
        public virtual EventResult HandleEvent(EventType type, object sender, object tag)
        {
            return EventResult.Unhandled;
        }

        /// <summary>
        /// Called when the user clicked the spell icon
        /// (or pressed the spell hot key).
        /// </summary>
        /// <returns>
        /// Whether the spell wants to receive BeginCast event.
        /// </returns>
        public virtual bool Trigger()
        {
            return false;
        }

        /// <summary>
        /// Perform the actual cast.
        /// </summary>
        public virtual void Cast()
        {
            if (Ready)
            {
                coolDownElapsed = 0;
            }
        }

        /// <summary>
        /// Cast the spell to a specified position.
        /// </summary>
        public virtual void Cast(Vector3 position)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Cast the spell to the target.
        /// </summary>
        public virtual void Cast(Entity target)
        {
            throw new InvalidOperationException();
        }
    }

    public class SpellButton : Button
    {
        /// <summary>
        /// Gets or sets the number in the top left corner.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the percentage drawed over the button.
        /// </summary>
        public float Percentage { get; set; }

        public bool ShowCount = true;
        public bool ShowPercentage = true;

        /// <summary>
        /// Gets or sets whether the spell is autoReleasable.
        /// </summary>
        public bool AutoCast { get; set; }

        /// <summary>
        /// The fade mask is divided into 5 parts to draw.
        /// </summary>
        /// <param name="sprite"></param>
        private void DrawFade()
        {
            if (Percentage <= 0)
            {
                return;
            }

            var baseAngle = Math.Atan2(DestinationRectangle.Width, DestinationRectangle.Height);

            var color = new Color(0, 0, 0, 85);
            var vertices = new VertexPositionColor[3]
            {
                new VertexPositionColor(new Vector3
                            (DestinationRectangle.Left + DestinationRectangle.Width / 2,
                            DestinationRectangle.Top + DestinationRectangle.Height/2, 0), color),
                new VertexPositionColor(new Vector3
                            (DestinationRectangle.Left, DestinationRectangle.Top, 0), color),
                new VertexPositionColor(new Vector3
                            (DestinationRectangle.Left + DestinationRectangle.Width / 2,
                            DestinationRectangle.Top, 0), color),
            };

            var indices = new ushort[3] { 0, 1, 2 };
            var angle = (100 - Percentage) / 100 * Math.PI * 2;

            // Part one: 0 ~ baseAngle
            if (angle > baseAngle)
            {
                Graphics2D.DrawPrimitive(vertices, indices);
            }
            else
            {
                vertices[1].Position.X = DestinationRectangle.Left + DestinationRectangle.Width / 2 -
                            (float)(DestinationRectangle.Height / 2 * Math.Tan(angle));
                Graphics2D.DrawPrimitive(vertices, indices);
                return;
            }

            // Part two: baseAngle ~ PI - baseAngle
            vertices[1].Position.X = DestinationRectangle.Left;
            vertices[1].Position.Y = DestinationRectangle.Bottom;
            vertices[2].Position.X = DestinationRectangle.Left;
            vertices[2].Position.Y = DestinationRectangle.Top;
            if (angle > Math.PI - baseAngle)
            {
                Graphics2D.DrawPrimitive(vertices, indices);
            }
            else
            {
                vertices[1].Position.Y = DestinationRectangle.Top + DestinationRectangle.Height / 2 +
                            (float)(Math.Tan(angle - Math.PI / 2) * DestinationRectangle.Width / 2);
                Graphics2D.DrawPrimitive(vertices, indices);
                return;
            }

            // Part three: PI - baseAngle ~ PI + baseAngle
            vertices[1].Position.X = DestinationRectangle.Right;
            vertices[1].Position.Y = DestinationRectangle.Bottom;
            vertices[2].Position.X = DestinationRectangle.Left;
            vertices[2].Position.Y = DestinationRectangle.Bottom;
            if (angle > Math.PI + baseAngle)
            {
                Graphics2D.DrawPrimitive(vertices, indices);
            }
            else
            {
                vertices[1].Position.X = DestinationRectangle.Left + DestinationRectangle.Width / 2 +
                            (float)(Math.Tan(angle) * DestinationRectangle.Height / 2);
                Graphics2D.DrawPrimitive(vertices, indices);
                return;
            }

            // Part four: PI + baseAngle ~ 2*PI - baseAngle
            vertices[1].Position.X = DestinationRectangle.Right;
            vertices[1].Position.Y = DestinationRectangle.Top;
            vertices[2].Position.X = DestinationRectangle.Right;
            vertices[2].Position.Y = DestinationRectangle.Bottom;
            if (angle > 2 * Math.PI - baseAngle)
            {
                Graphics2D.DrawPrimitive(vertices, indices);
            }
            else
            {
                vertices[1].Position.Y = DestinationRectangle.Top + DestinationRectangle.Height / 2 -
                            (float)(Math.Tan(angle - 3 * Math.PI / 2) * DestinationRectangle.Width / 2);
                Graphics2D.DrawPrimitive(vertices, indices);
                return;
            }

            // Part five: 2*PI - baseAngle ~ 2*PI
            vertices[1].Position.X = DestinationRectangle.Left + DestinationRectangle.Width / 2;
            vertices[1].Position.Y = DestinationRectangle.Top;
            vertices[2].Position.X = DestinationRectangle.Right;
            vertices[2].Position.Y = DestinationRectangle.Top;
            if (angle >= 2 * Math.PI)
            {
                Graphics2D.DrawPrimitive(vertices, indices);
            }
            else
            {
                vertices[1].Position.X = DestinationRectangle.Left + DestinationRectangle.Width / 2 +
                            (float)(Math.Tan(Math.PI * 2 - angle) * DestinationRectangle.Height / 2);
                Graphics2D.DrawPrimitive(vertices, indices);
                return;
            }
        }

        /// <summary>
        /// Draw.
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="sprite"></param>
        public override void Draw(GameTime gameTime, SpriteBatch sprite)
        {
            base.Draw(gameTime, sprite);

            if (ShowPercentage)
            {
                DrawFade();
            }

            if (Count > 1 && ShowCount)
            {
                Graphics2D.DrawShadowedString(Count.ToString(), 15f / 23,
                                                new Vector2(DestinationRectangle.X + DestinationRectangle.Width / 15,
                                                            DestinationRectangle.Y + DestinationRectangle.Height / 30),
                                                Color.White, Color.Black);
            }
        }
    }

    public class SpellTraining : Spell
    {
        /// <summary>
        /// Gets the type of unit that's going to be trained.
        /// </summary>
        public string Type { get; }

        private Building ownerBuilding;
        public bool ShowCount = true;

        protected Action<Spell, Building> Complete;

        protected override void OnOwnerChanged()
        {
            ownerBuilding = Owner as Building;

            if (ownerBuilding == null)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Gets or sets the number of units that going to be trained.
        /// </summary>
        public int Count
        {
            get => spellButton != null ? spellButton.Count : 0;
            set
            {
                if (spellButton != null)
                {
                    spellButton.Count = value;
                }
            }
        }

        public SpellTraining(GameWorld world, string type, Building owner)
            : base(world)
        {
            Type = type ?? throw new ArgumentNullException();
            ownerBuilding = owner;
        }

        public override void Deserialize(XmlElement xml)
        {
            if (xml.HasAttribute("TrainingTime"))
            {
                CoolDown = float.Parse(xml.GetAttribute("TrainingTime"));
            }

            base.Deserialize(xml);
        }

        protected override SpellButton CreateUIElement(GameUI ui)
        {
            SpellButton button = base.CreateUIElement(ui);

            // Add right click event handler
            button.RightClick += (sender, e) =>
            {
                if (ownerBuilding != null)
                {
                    ownerBuilding.CancelTraining(this);
                    Audios.Play("OK");
                }
            };

            return button;
        }

        public override bool Trigger()
        {
            if (Player.LocalPlayer != null &&
                Player.LocalPlayer.CurrentGroup != null &&
                Player.LocalPlayer.CurrentGroup.Count > 0 &&
                Player.LocalPlayer.CurrentGroup[0] is Building)
            {
                // Find a building with the minimum number of pending request
                var min = int.MaxValue;
                Building building = null;

                foreach (GameObject o in Player.LocalPlayer.CurrentGroup)
                {
                    if (o is Building b && b.QueuedSpells.Count < min)
                    {
                        min = b.QueuedSpells.Count;
                        building = b;
                    }
                }

                if (building != null && building.TrainUnit(Type))
                {
                    Audios.Play("OK");

                    if (Player.LocalPlayer.CurrentGroup.Count > 1 && !building.CanTrain(Type))
                    {
                        Enable = false;
                    }
                }
                else
                {
                    Audios.Play("Invalid");
                }
            }

            return false;
        }

        protected override TipBox CreateTipBox()
        {
            GameDefault def = GameDefault.Singleton;
            var height = 6;
            TextField gold = null, lumber = null, food = null;
            var title = new TextField(Name + "  [" + Hotkey.ToString() + "]",
                                            16f / 23, Color.Gold, new Rectangle(6, 6, 210, 30));
            height += 30;
            var content = new TextField(Description, 15f / 23, Color.White,
                                                new Rectangle(6, 30, 210, 100));
            height += content.RealHeight + 10;
            if (def.GetGold(Type) != 0)
            {
                gold = new TextField("Gold:         " + def.GetGold(Type).ToString(),
                                            15f / 23, Color.Gold,
                                            new Rectangle(6, height, 210, 29));
                height += gold.RealHeight;
            }

            if (def.GetLumber(Type) != 0)
            {
                lumber = new TextField("Lumber:      " + def.GetLumber(Type).ToString(),
                                            15f / 23, Color.Green,
                                            new Rectangle(6, height, 210, 29));
                height += lumber.RealHeight;
            }

            if (def.GetFood(Type) != 0)
            {
                food = new TextField("Food:         " + def.GetFood(Type).ToString(),
                                            15f / 23, Color.Magenta,
                                            new Rectangle(6, height, 210, 19));
                height += food.RealHeight;
            }

            var spellTip = new TipBox(220, height + 10);
            spellTip.Add(title);
            spellTip.Add(content);
            if (gold != null)
            {
                spellTip.Add(gold);
            }

            if (lumber != null)
            {
                spellTip.Add(lumber);
            }

            if (food != null)
            {
                spellTip.Add(food);
            }

            return spellTip;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Show count...
            if (ownerBuilding != null && ownerBuilding.Owner is LocalPlayer)
            {
                var multipleBuildings = false;
                if (ShowCount)
                {
                    if (Player.LocalPlayer != null &&
                        Player.LocalPlayer.CurrentGroup != null &&
                        Player.LocalPlayer.CurrentGroup.Count > 1 &&
                        Player.LocalPlayer.CurrentGroup[0] is Building)
                    {
                        if (spellButton != null)
                        {
                            spellButton.ShowCount = false;
                            spellButton.ShowPercentage = false;
                            multipleBuildings = true;
                        }
                    }
                    else
                    {
                        if (spellButton != null)
                        {
                            spellButton.ShowCount = true;
                            spellButton.ShowPercentage = true;
                        }
                    }
                }
                else
                {
                    spellButton.ShowCount = false;
                }

                // Update enable states
                if (ownerBuilding != null)
                {
                    Enable = multipleBuildings
                        ? ownerBuilding.CanTrain(Type)
                        : ownerBuilding.CanTrain(Type) || !Ready ||
                                 ownerBuilding.QueuedSpells.Contains(this);
                }
            }

            // Unit trained
            if (Ready && ownerBuilding.QueuedSpells.Count > 0 &&
                         ownerBuilding.QueuedSpells.Peek() == this)
            {
                ownerBuilding.QueuedSpells.Dequeue();
                Count--;

                // Start next spell?
                if (ownerBuilding.QueuedSpells.Count > 0)
                {
                    if (ownerBuilding.QueuedSpells.Peek() is SpellTraining)
                    {
                        (ownerBuilding.QueuedSpells.Peek() as SpellTraining).CoolDownElapsed = 0;
                    }
                }

                Complete?.Invoke(this, ownerBuilding);

                OnComplete();
            }
        }

        protected virtual void OnComplete()
        {
            if (world.Create(Type) is Charactor c)
            {
                if (ownerBuilding.Owner != null)
                {
                    ownerBuilding.Owner.UnmarkFutureObject(Type);
                }

                c.Position = ownerBuilding.Position + ownerBuilding.SpawnPoint;
                c.Owner = ownerBuilding.Owner;
                c.Owner.Food -= c.Food;
                c.Fall();
                world.Add(c);

                if (c.IsHero && spellButton != null)
                {
                    spellButton.Visible = false;
                }

                foreach (var o in ownerBuilding.RallyPoints)
                {
                    if (o is Entity)
                    {
                        c.PerformAction(o as Entity, true);
                    }
                    else if (o is Vector3 vector)
                    {
                        c.PerformAction(vector, true);
                    }
                }
            }
        }
    }

    public class SpellUpgrade : SpellTraining
    {
        public SpellUpgrade(GameWorld world, string type)
            : base(world, type, null)
        {
            GameDefault.Singleton.SetUnique(Type);
        }

        public SpellUpgrade(GameWorld world, string type, Action<Spell, Building> onComplete)
            : base(world, type, null)
        {
            GameDefault.Singleton.SetUnique(Type);
            Complete = onComplete;
        }

        protected override void OnComplete()
        {
            if (Owner.Owner is LocalPlayer)
            {
                Audios.Play("UpgradeComplete");
            }
        }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Uniform delegate")]
    public static class Upgrades
    {
        public static void LiveOfNature(Spell spell, Building owner)
        {
            if (owner != null && owner.Owner != null)
            {
                foreach (GameObject o in owner.Owner.EnumerateObjects("Lumbermill"))
                {
                    if (o is Lumbermill)
                    {
                        (o as Lumbermill).LiveOfNatureResearched();
                    }
                }

                owner.Owner.MarkAvailable("LiveOfNature");
            }
        }

        public static void PunishOfNature(Spell spell, Building owner)
        {
            if (owner != null && owner.Owner != null)
            {
                owner.Owner.MarkAvailable("PunishOfNatureUpgrade");

                foreach (GameObject o in owner.Owner.EnumerateObjects("FireSorceress"))
                {
                    o.AddSpell("PunishOfNature");
                }
            }
        }

        public static void Attack(Spell spell, Building owner)
        {
            if (owner != null && owner.Owner != null)
            {
                owner.Owner.AttackPoint = 20;
                owner.Owner.MarkAvailable("AttackUpgrade");

                foreach (GameObject o in owner.Owner.EnumerateObjects())
                {
                    if (o.AttackPoint.X > 0)
                    {
                        o.AttackPoint.X += 20;
                        o.AttackPoint.Y += 20;
                    }
                }
            }
        }

        public static void Defense(Spell spell, Building owner)
        {
            if (owner != null && owner.Owner != null)
            {
                owner.Owner.DefensePoint = 20;
                owner.Owner.MarkAvailable("DefenseUpgrade");

                foreach (GameObject o in owner.Owner.EnumerateObjects())
                {
                    if (o.DefensePoint.X > 0)
                    {
                        o.DefensePoint.X += 20;
                        o.DefensePoint.Y += 20;
                    }
                }
            }
        }
    }

    /// <summary>
    /// An interface for the construct spell to interact with other entities.
    /// A placeable must also be a derived class of Entity.
    /// </summary>
    public interface IPlaceable
    {
        /// <summary>
        /// Called before the user chooses where to place the object.
        /// </summary>
        bool BeginPlace();

        /// <summary>
        /// Gets whether the current position if placable.
        /// </summary>
        bool IsLocationPlacable();

        /// <summary>
        /// Place the entity.
        /// </summary>
        void Place();

        /// <summary>
        /// Called when the construct request was canceled.
        /// </summary>
        void CancelPlace();
    }

    /// <summary>
    /// A spell encapsulating the behavior of constructing a building.
    /// </summary>
    public class SpellConstruct : Spell
    {
        public bool AutoReactivate;
        private int step;
        private readonly string entityType;
        private readonly Input input;
        private Entity entity;
        private BaseEntity baseEntity;
        private IPlaceable placeable;

        public SpellConstruct(GameWorld world, string entityType)
            : base(world)
        {
            input = BaseGame.Singleton.Input;
            this.entityType = entityType;
        }

        public SpellConstruct(GameWorld world, BaseEntity baseEntity)
            : base(world)
        {
            if (null == baseEntity || null == world)
            {
                throw new ArgumentNullException();
            }

            input = BaseGame.Singleton.Input;
            this.baseEntity = baseEntity;
            entityType = baseEntity.ClassID;
        }

        public override bool Trigger()
        {
            hasCasted = false;
            step = 0;

            if (baseEntity == null)
            {
                baseEntity = world.Create(entityType) as BaseEntity;
            }

            if (baseEntity != null)
            {
                entity = baseEntity as Entity;
                placeable = baseEntity as IPlaceable;

                // Set owner if it's a building
                if (Player.LocalPlayer != null && baseEntity is Building building)
                {
                    building.Owner = Player.LocalPlayer;
                }

                if (placeable != null && !placeable.BeginPlace())
                {
                    Spell.EndSpell();
                    return false;
                }
            }

            Audios.Play("OK");
            return baseEntity != null;
        }

        protected override TipBox CreateTipBox()
        {
            GameDefault def = GameDefault.Singleton;
            var height = 6;
            var title = new TextField(Name + "  [" + Hotkey.ToString() + "]",
                                            16f / 23, Color.Gold, new Rectangle(6, 6, 210, 30));
            height += 30;
            var content = new TextField(Description, 15f / 23, Color.White,
                                                new Rectangle(6, 30, 210, 100));
            height += content.RealHeight + 10;
            TextField gold = null, lumber = null;
            if (def.GetGold(entityType) != 0)
            {
                gold = new TextField("Gold:         " + def.GetGold(entityType).ToString(),
                                            15f / 23, Color.Gold, new Rectangle(6, height, 210, 29));
                height += gold.RealHeight;
            }

            if (def.GetLumber(entityType) != 0)
            {
                lumber = new TextField("Lumber:      " + def.GetLumber(entityType).ToString(),
                                            15f / 23, Color.Green, new Rectangle(6, height, 210, 29));
                height += lumber.RealHeight;
            }

            var spellTip = new TipBox(220, height + 10);
            spellTip.Add(title);
            spellTip.Add(content);
            if (gold != null)
            {
                spellTip.Add(gold);
            }

            if (lumber != null)
            {
                spellTip.Add(lumber);
            }

            return spellTip;
        }

        public override void UpdateCast(GameTime gameTime)
        {
            // Update entity position
            if (step == 0)
            {
                if (world.TargetPosition.HasValue)
                {
                    baseEntity.Position = world.TargetPosition.Value;
                }
            }

            // Adjusting entity rotation
            else if (step == 1 && input.Mouse.LeftButton == ButtonState.Pressed && entity != null)
            {
                var rotation = beginDropRotation + MathHelper.PiOver2 + (float)Math.Atan2(
                                -(input.MousePosition.Y - beginDropPosition.Y),
                                 input.MousePosition.X - beginDropPosition.X);

                entity.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation);

                if (entity is Building)
                {
                    (entity as Building).RotationZ = rotation;
                }
                else if (entity is Goldmine)
                {
                    (entity as Goldmine).RotationZ = rotation;
                }
            }

            baseEntity.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            baseEntity.Draw(gameTime);
        }

        private Point beginDropPosition;
        private float beginDropRotation;
        private bool hasCasted;

        public override EventResult HandleEvent(EventType type, object sender, object tag)
        {
            // Cancel build if right clicked for Esc is pressed
            Keys key = (tag is Keys?) ? (tag as Keys?).Value : Keys.None;
            if ((type == EventType.KeyUp && key == Keys.Escape) ||
                 type == EventType.RightButtonUp || type == EventType.RightButtonDown ||
                hasCasted && type == EventType.KeyUp && (key == Keys.LeftShift) || key == Keys.RightShift)
            {
                if (placeable != null)
                {
                    placeable.CancelPlace();
                }

                input.Uncapture();
                Spell.EndSpell();
                return EventResult.Handled;
            }

            // Start build
            if (type == EventType.LeftButtonDown && step == 0)
            {
                // Click on the minimap to change location
                if (GameUI.Singleton.Minimap.MapToWorld(input.MousePosition).HasValue)
                {
                    return EventResult.Unhandled;
                }

                // Check if the current position if placable
                if (placeable != null && !placeable.IsLocationPlacable())
                {
                    Audios.Play("CannotBuild", Audios.Channel.Interface, null);
                    GameUI.Singleton.PushMessage("Can not build there!", MessageType.Unavailable, Color.White);
                    return EventResult.Handled;
                }

                step = 1;
                input.Capture(this);

                if (entity != null)
                {
                    beginDropRotation = 0;
                    beginDropPosition = input.MousePosition;
                    beginDropPosition.Y -= 10;
                }

                return EventResult.Handled;
            }

            // End build
            if (type == EventType.LeftButtonUp && step == 1)
            {
                // Make sure we uncapture the input and end the spell
                input.Uncapture();

                if (placeable != null && !placeable.IsLocationPlacable())
                {
                    step = 0;
                    Audios.Play("CannotBuild", Audios.Channel.Interface, null);
                    GameUI.Singleton.PushMessage("Can not build there...", MessageType.Unavailable, Color.White);
                    return EventResult.Handled;
                }

                // Get builder
                if (baseEntity is Building)
                {
                    Worker builder = null;
                    Worker firstBuilder = null;

                    foreach (GameObject o in Player.LocalPlayer.CurrentGroup)
                    {
                        if (firstBuilder == null)
                        {
                            firstBuilder = o as Worker;
                        }

                        if (o is Worker && !(o.State is StateConstruct))
                        {
                            builder = o as Worker;
                            break;
                        }
                    }

                    // If no idle builder is found, use the first builder
                    if (builder == null)
                    {
                        builder = firstBuilder;
                    }
(baseEntity as Building).Builder = builder;
                }

                if (placeable != null)
                {
                    placeable.Place();
                }

                world.Add(baseEntity);

                // Reset everything
                entity = null;
                baseEntity = null;
                placeable = null;
                Spell.EndSpell();

                // Auto reactivate
                if (AutoReactivate || input.IsShiftPressed)
                {
                    Spell.Cast(this);
                    hasCasted = true;
                }

                return EventResult.Handled;
            }

            return EventResult.Unhandled;
        }
    }

    public class SpellAttack : Spell
    {
        /// <summary>
        /// Creates a new team attack.
        /// </summary>
        public SpellAttack(GameWorld world)
            : base(world)
        {
            Name = "Attack";
            Hotkey = Keys.A;
            Icon = Icon.FromTiledTexture(11);
        }

        public override bool Trigger()
        {
            if (Helper.Random.Next(20) == 0)
            {
                Audios.Play("Hawk");
            }
            else
            {
                Audios.Play("OK");
            }

            Cursors.SetCursor(Cursors.TargetRed);
            return true;
        }

        public override EventResult HandleEvent(EventType type, object sender, object tag)
        {
            if (type == EventType.LeftButtonDown)
            {
                var input = sender as Input;
                // Checks if we have clicked the minimap
                Vector3? minimapPoint = GameUI.Singleton.Minimap.MapToWorld(input.MousePosition);

                if (minimapPoint.HasValue)
                {
                    Player.LocalPlayer.AttackTo(minimapPoint.Value);
                    Cursors.SetCursor(Cursors.Default);
                    Spell.EndSpell();
                    return EventResult.Handled;
                }

                // Checks if we've cliked the UI
                if (GameUI.Singleton.Overlaps(input.MousePosition))
                {
                    return EventResult.Handled;
                }

                // Attack entity
                var picked = world.Pick() as GameObject;

                if (picked != null && world.FogOfWar != null &&
                                      world.FogOfWar.Contains(picked.Position.X, picked.Position.Y))
                {
                    picked = null;
                }

                if (picked != null)
                {
                    if (picked.IsAlive)
                    {
                        Player.LocalPlayer.AttackTo(picked);
                        Cursors.SetCursor(Cursors.Default);
                        Spell.EndSpell();
                    }
                    else
                    {
                        Audios.Play("Invalid");
                    }

                    return EventResult.Handled;
                }

                // Attack to position
                Vector3? point = world.Landscape.Pick();

                if (point.HasValue)
                {
                    Player.LocalPlayer.AttackTo(point.Value);
                    Cursors.SetCursor(Cursors.Default);
                    Spell.EndSpell();

                    Vector3 location = point.Value;
                    location.Z = world.Landscape.GetHeight(location.X, location.Y);
                    GameUI.Singleton.SetCursorFocus(location, Color.Red);
                }

                return EventResult.Handled;
            }

            if (type == EventType.RightButtonDown ||
               (type == EventType.KeyDown && (tag is Keys?) && (tag as Keys?).Value == Keys.Escape))
            {
                // Cancel attack
                Cursors.SetCursor(Cursors.Default);
                Spell.EndSpell();
                return EventResult.Handled;
            }

            return EventResult.Unhandled;
        }
    }

    public class SpellMove : Spell
    {
        public SpellMove(GameWorld world)
            : base(world)
        {
            Name = "Move";
            Hotkey = Keys.M;
            Icon = Icon.FromTiledTexture(8);
        }

        public override bool Trigger()
        {
            Audios.Play("OK");
            Cursors.SetCursor(Cursors.TargetGreen);
            return true;
        }

        public override EventResult HandleEvent(EventType type, object sender, object tag)
        {
            if (type == EventType.LeftButtonDown)
            {
                var input = sender as Input;
                // Checks if we have clicked the minimap
                Vector3? minimapPoint = GameUI.Singleton.Minimap.MapToWorld(input.MousePosition);

                if (minimapPoint.HasValue)
                {
                    Player.LocalPlayer.MoveTo(minimapPoint.Value);
                    Cursors.SetCursor(Cursors.Default);
                    Spell.EndSpell();
                    return EventResult.Handled;
                }

                // Checks if we've cliked the UI
                if (GameUI.Singleton.Overlaps(input.MousePosition))
                {
                    return EventResult.Handled;
                }

                // Attack entity
                var picked = world.Pick() as GameObject;

                if (picked != null && world.FogOfWar != null &&
                                      world.FogOfWar.Contains(picked.Position.X, picked.Position.Y))
                {
                    picked = null;
                }

                if (picked != null && picked.IsAlive)
                {
                    Player.LocalPlayer.MoveTo(picked);
                    Cursors.SetCursor(Cursors.Default);
                    Spell.EndSpell();
                    return EventResult.Handled;
                }

                // Attack to position
                Vector3? point = world.Landscape.Pick();

                if (point.HasValue)
                {
                    Player.LocalPlayer.MoveTo(point.Value);
                    Cursors.SetCursor(Cursors.Default);
                    Spell.EndSpell();

                    Vector3 location = point.Value;
                    location.Z = world.Landscape.GetHeight(location.X, location.Y);
                    GameUI.Singleton.SetCursorFocus(location, Color.Green);
                }

                return EventResult.Handled;
            }

            if (type == EventType.RightButtonDown ||
               (type == EventType.KeyDown && (tag is Keys?) && (tag as Keys?).Value == Keys.Escape))
            {
                // Cancel attack
                Cursors.SetCursor(Cursors.Default);
                Spell.EndSpell();
                return EventResult.Handled;
            }

            return EventResult.Unhandled;
        }
    }

    /// <summary>
    /// Any spell againest units or buildings should derive from
    /// SpellCombat and use StateAttack to trigger the event.
    /// </summary>
    public class SpellCombat : Spell
    {
        /// <summary>
        /// Common stuff.
        /// </summary>
        public GameObject Target;

        /// <summary>
        /// Gets or sets the minimum value of the attack range.
        /// </summary>
        public float MinimumRange;

        /// <summary>
        /// Gets or sets the maximum value of the attack range.
        /// </summary>
        public float MaximumRange = 8;

        /// <summary>
        /// Creates a new combat spell.
        /// </summary>
        public SpellCombat(GameWorld world, GameObject owner)
            : base(world)
        {
            Owner = owner ?? throw new ArgumentNullException();
        }

        protected override void OnOwnerChanged()
        {
            if (Owner == null)
            {
                throw new ArgumentNullException();
            }

            MinimumRange = Owner.AttackRange.X;
            MaximumRange = Owner.AttackRange.Y;
            CoolDown = Owner.AttackDuration;
        }

        /// <summary>
        /// Gets whether the target is within the attack range.
        /// </summary>
        public virtual bool TargetWithinRange(Entity target)
        {
            Vector2 position;
            position.X = Owner.Position.X;
            position.Y = Owner.Position.Y;

            var distance = target.Outline.DistanceTo(position);
            distance -= Owner.Outline.Radius;
            if (distance < 0)
            {
                distance = 0;
            }

            return distance >= MinimumRange && distance <= MaximumRange;
        }

        /// <summary>
        /// Gets whether the target can be attacked by this spell.
        /// </summary>
        public virtual bool CanAttakTarget(GameObject target)
        {
            return target != null && target.IsAlive &&
                   target.Visible && !target.InFogOfWar && target != Owner;// && Owner.IsOpponent(target);
        }

        public override void Cast(Entity target)
        {
            var gameObject = target as GameObject;

            if (Ready && CanAttakTarget(gameObject) && TargetWithinRange(target))
            {
                if (gameObject != null && gameObject.Owner is LocalPlayer)
                {
                    (gameObject.Owner as LocalPlayer).AddAttacker(Owner);
                }

                Owner.TriggerAttack(target);
                base.Cast();
            }
        }
    }

    public class SpellSummon : Spell
    {
        private readonly string type;

        public SpellSummon(GameWorld world, string type)
            : base(world, type)
        {
            this.type = type;
        }

        public override void Update(GameTime gameTime)
        {
            Enable = true;
            if (Owner != null && Owner.Owner.IsAvailable(type))
            {
                Enable = false;
            }

            base.Update(gameTime);
        }

        public override bool Trigger()
        {
            Cast();
            return false;
        }

        public override void Cast()
        {
            if (Owner != null)
            {
                var wo = world.Create(type);
                wo.Position = Owner.Position;

                if (wo is GameObject)
                {
                    (wo as GameObject).Owner = Owner.Owner;
                }

                if (wo is Charactor && Owner is Charactor)
                {
                    (wo as Charactor).Facing = (Owner as Charactor).Facing;
                }

                world.Add(wo);

                // Add some effect
                float radius = 10;

                if (wo is GameObject)
                {
                    radius = (wo as GameObject).SelectionAreaRadius;
                }

                var effect = new EffectSpawn(world, wo.Position, radius, "Summon");

                world.Add(effect);

                Audios.Play("Hellfire");
            }

            base.Cast();
        }
    }

    public class SpellPunishOfNature : Spell
    {
        private const float Duration = 60;
        private float elapsedTime;
        private EffectPunishOfNature effect;

        public SpellPunishOfNature(GameWorld world)
            : base(world, "PunishOfNature")
        {
        }

        public override bool Trigger()
        {
            if (Ready)
            {
                Audios.Play("OK");
                Audios.Play("Punish");
                Cast();
            }
            else
            {
                Audios.Play("Invalid");
            }

            return false;
        }

        public override void Cast()
        {
            if (Owner != null)
            {
                effect = new EffectPunishOfNature(world, Owner.Position);
                elapsedTime = 0;
                base.Cast();
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (effect != null)
            {
                effect.Update(gameTime);

                var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
                elapsedTime += elapsedSeconds;

                foreach (var wo in world.GetNearbyObjectsPrecise(effect.Position, EffectPunishOfNature.Radius))
                {
                    if (wo is not Charactor o || o.Owner == null)
                    {
                        continue;
                    }

                    if (Owner.IsOpponent(o))
                    {
                        var damage = (0.2f + o.Owner.EnvironmentLevel * 0.6f) * 30;
                        o.Health -= damage * elapsedSeconds;
                    }
                    else if (Owner.IsAlly(o))
                    {
                        var health = (0.2f + (1 - o.Owner.EnvironmentLevel) * 0.6f) * 40;
                        o.Health += health * elapsedSeconds;
                        if (o.Health < o.MaximumHealth)
                        {
                            o.ShowGlow = true;
                        }
                    }
                }

                if (elapsedTime >= Duration)
                {
                    effect = null;
                }
            }

            base.Update(gameTime);
        }
    }
}
