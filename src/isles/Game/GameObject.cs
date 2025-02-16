// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles;

public abstract class GameObject : Entity, ISelectable
{
    public Player Owner { get; set; }
    public float Priority { get; set; }

    private Icon icon;

    public int? Icon { get; set; }

    public int? Snapshot { get; set; }

    public Icon SnapshotIcon { get; private set; }

    public static Texture2D SnapshotTexture
    {
        get
        {
            if (snapshotTexture == null)
            {
                snapshotTexture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/Snapshots.png");
            }

            return snapshotTexture;
        }
    }

    private static Texture2D snapshotTexture;

    /// <summary>
    /// Gets or sets the profile button.
    /// </summary>
    public SpellButton ProfileButton { get; set; }

    /// <summary>
    /// Gets or sets the tip for the object.
    /// </summary>
    public TipBox Tip { get; set; }

    /// <summary>
    /// Gets or sets the view distance of this game object.
    /// </summary>
    public float ViewDistance { get; set; } = 100;

    /// <summary>
    /// Gets or sets the radius of selection circle.
    /// </summary>
    public float AreaRadius { get; set; } = 10;

    /// <summary>
    /// Gets or sets the sound effect associated with this game object.
    /// </summary>
    public string Sound { get; set; }

    /// <summary>
    /// Gets or sets the sound effect for combat.
    /// </summary>
    public string SoundCombat { get; set; }

    /// <summary>
    /// Gets or sets the sound effect for die.
    /// </summary>
    public string SoundDie { get; set; }

    /// <summary>
    /// Gets or sets the health of this game object.
    /// </summary>
    public float Health
    {
        get => health;

        set
        {
            if (value > MaxHealth)
            {
                value = MaxHealth;
            }

            // Cannot reborn
            if (value > 0 && health <= 0)
            {
                value = 0;
            }

            if (value <= 0 && health > 0)
            {
                // Clear all spells
                foreach (Spell spell in SpellList)
                {
                    spell.Enable = false;
                }

                SpellList.Clear();

                if (SoundDie != null && ShouldDrawModel)
                {
                    Audios.Play(SoundDie, this);
                }

                if (Highlighted)
                {
                    Highlighted = false;
                }

                if (Selected)
                {
                    Selected = false;
                }

                OnDie();
            }

            if (value < health && health > 0 && MaxHealth > 0 && Owner is LocalPlayer)
            {
                Audios.Play("UnderAttack", Audios.Channel.UnderAttack, null);
            }

            if (value < 0)
            {
                value = 0;
            }

            health = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum health of this charactor.
    /// </summary>
    public float MaxHealth { get; set; }

    /// <summary>
    /// Gets whether the game object is alive.
    /// </summary>
    public bool IsAlive => health > 0 || (health <= 0 && MaxHealth <= 0);

    public float health;

    public string[] Units { get; set; } = Array.Empty<string>();

    public string[] Buildings { get; set; } = Array.Empty<string>();

    public string[] Summons { get; set; } = Array.Empty<string>();

    public string[] Upgrades { get; set; } = Array.Empty<string>();

    public List<Spell> SpellList = new();

    public bool Selected
    {
        get => selected;

        set
        {
            selected = value;

            if (selected)
            {
                OnSelect(GameUI.Singleton);
            }
            else
            {
                OnDeselect(GameUI.Singleton);
            }
        }
    }

    private bool selected;

    public bool Highlighted
    {
        get => highlighted;

        set
        {
            highlighted = value;

            if (GameModel != null)
            {
                GameModel.Glow = highlighted ? Vector3.One : Vector3.Zero;
            }

            if (highlighted && Owner != null && ShouldDrawModel)
            {
                if (Tip == null)
                {
                    Tip = CreateTipBox();
                }

                GameUI.Singleton.TipBoxContainer.Add(Tip);
            }
            else if (!highlighted && Owner != null)
            {
                GameUI.Singleton.TipBoxContainer.Remove(Tip);
            }
        }
    }

    private bool highlighted;

    public bool Focused
    {
        get => focused;

        set
        {
            focused = value;

            if (value)
            {
                ShowSpells(GameUI.Singleton);
            }
        }
    }

    private bool focused;

    /// <summary>
    /// Gets the top center position of the game object.
    /// </summary>
    public Vector3 TopCenter
    {
        get
        {
            Vector3 v = Position;
            v.Z = BoundingBox.Max.Z;
            return v;
        }
    }

    /// <summary>
    /// Min/Max attack point.
    /// </summary>
    public Vector2 AttackPoint { get; set; }

    /// <summary>
    /// Min/Max defense point.
    /// </summary>
    public Vector2 DefensePoint { get; set; }

    /// <summary>
    /// Gets or sets the min/max attack range of this charactor.
    /// </summary>
    public Vector2 AttackRange { get; set; }

    /// <summary>
    /// Gets or sets the duration of each individual attack.
    /// </summary>
    public float AttackDuration { get; set; }

    /// <summary>
    /// Flash related stuff.
    /// </summary>
    public const float FlashDuration = 0.5f;
    public float flashElapsedTime = FlashDuration + 0.1f;

    public Dictionary<string, string> Attachments { get; set; } = new();

    public List<KeyValuePair<GameModel, int>> Attachment = new();

    public void Flash()
    {
        flashElapsedTime = 0;
    }

    public override void OnDeserialized()
    {
        base.OnDeserialized();

        health = MaxHealth;

        foreach (var (bone, model) in Attachments)
        {
            var attachPoint = GameModel.GetBone(bone);
            if (attachPoint < 0)
            {
                throw new Exception("Bone '" + bone + "' do not exist in model '" + Model + "'.");
            }

            Attachment.Add(new KeyValuePair<GameModel, int>(new(model), attachPoint));
        }

        if (Snapshot != null)
        {
            SnapshotIcon = Isles.Icon.FromTiledTexture(Snapshot.Value, 8, 4, SnapshotTexture);
        }

        if (Icon != null)
        {
            var iconIndex = Icon.Value;
            icon = Isles.Icon.FromTiledTexture(iconIndex);

            ProfileButton = new SpellButton
            {
                Texture = icon.Texture,
                SourceRectangle = icon.Region,
                Hovered = Isles.Icon.RectangeFromIndex(iconIndex + 1),
                Pressed = Isles.Icon.RectangeFromIndex(iconIndex + 2),
                Anchor = Anchor.BottomLeft,
                ScaleMode = ScaleMode.ScaleY,
            };

            ProfileButton.Click += (sender, e) => Player.LocalPlayer.Focus(this);

            ProfileButton.DoubleClick += (sender, e) => Player.LocalPlayer.SelectGroup(this);
        }

        foreach (var building in Buildings)
        {
            AddSpell(new SpellConstruct(building));
        }

        foreach (var unit in Units)
        {
            AddSpell(new SpellTraining(unit));
        }

        foreach (var upgrade in Upgrades)
        {
            AddSpell(new SpellUpgrade(upgrade));
        }

        foreach (var summon in Summons)
        {
            AddSpell(new SpellSummon(summon));
        }
    }

    public void AddSpell(Spell spell)
    {
        spell.Owner = this;
        OnCreateSpell(spell);
        SpellList.Add(spell);

        if (Selected && Focused)
        {
            ShowSpells(GameUI.Singleton);
        }
    }

    protected virtual void OnCreateSpell(Spell spell) { }

    public virtual void PerformAction(Vector3 position, bool queueAction) { }

    public virtual void PerformAction(Entity entity, bool queueAction) { }

    public override void OnCreate()
    {
        UpdateFogOfWar();

        if (Owner != null)
        {
            // Model.Tint = owner.TeamColor.ToVector3();
            if (AttackPoint.X > 0)
            {
                AttackPoint += Owner.AttackPoint * Vector2.One;
            }

            if (DefensePoint.X > 0)
            {
                DefensePoint += Owner.DefensePoint * Vector2.One;
            }
        }
    }

    public override bool IsPickable => IsAlive && base.IsPickable;

    public override bool Intersects(BoundingFrustum frustum)
    {
        return IsAlive && base.Intersects(frustum);
    }

    /// <summary>
    /// Gets the relationship with another game object.
    /// </summary>
    public PlayerRelation GetRelation(GameObject gameObject)
    {
        return Owner == null || gameObject == null || gameObject.Owner == null ? PlayerRelation.Neutral : Owner.GetRelation(gameObject.Owner);
    }

    public bool IsAlly(GameObject gameObject)
    {
        return GetRelation(gameObject) == PlayerRelation.Ally;
    }

    public bool IsOpponent(GameObject gameObject)
    {
        return GetRelation(gameObject) == PlayerRelation.Opponent;
    }

    public override void Update(GameTime gameTime)
    {
        // Update spells
        foreach (Spell spell in SpellList)
        {
            spell.Update(gameTime);
        }

        // Update fog of war state for other players
        UpdateFogOfWar();

        // Snap to ground
        Position = new(Position.X, Position.Y, World.Heightmap.GetHeight(Position.X, Position.Y));

        base.Update(gameTime);

        // Update attachments after model is updated
        foreach (var attach in Attachment)
        {
            if (attach.Value >= 0)
            {
                attach.Key.Transform = GameModel.GetBoneTransform(attach.Value);
            }
        }

        // Draw fog of war
        if (Owner != null && Visible && World.FogOfWar != null &&
            (Owner is LocalPlayer || World.Game.Settings.RevealMap))
        {
            DrawFogOfWar();
        }
    }

    private void UpdateFogOfWar()
    {
        if (Visible && Owner is not LocalPlayer && World.FogOfWar != null)
        {
            var nowState = World.FogOfWar.Contains(Position.X, Position.Y);

            if (nowState && !InFogOfWar)
            {
                // Enter fog of war
                EnterFogOfWar();
                InFogOfWar = true;
            }
            else if (!nowState && InFogOfWar)
            {
                // Leave fog of war
                LeaveFogOfWar();
                InFogOfWar = false;
            }
        }
    }

    public static Texture2D SelectionAreaTexture
    {
        get
        {
            if (selectionAreaTexture == null || selectionAreaTexture.IsDisposed)
            {
                selectionAreaTexture = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/SelectionArea.png");
            }

            return selectionAreaTexture;
        }
    }

    private static Texture2D selectionAreaTexture;

    public static Texture2D SelectionAreaTextureLarge
    {
        get
        {
            if (selectionAreaTextureLarge == null || selectionAreaTexture.IsDisposed)
            {
                selectionAreaTextureLarge = BaseGame.Singleton.TextureLoader.LoadTexture("data/ui/SelectionAreaLarge.png");
            }

            return selectionAreaTextureLarge;
        }
    }

    private static Texture2D selectionAreaTextureLarge;

    public bool ShowStatus = true;

    /// <summary>
    /// Gets or sets whether this game object is currently in the fog of war.
    /// </summary>
    public bool InFogOfWar;

    /// <summary>
    /// Gets or sets whether this game object will be shown in the fog of war.
    /// </summary>
    public bool VisibleInFogOfWar = true;

    /// <summary>
    /// Gets whether models should be drawed.
    /// </summary>
    public bool ShouldDrawModel => !InFogOfWar && Visible && WithinViewFrustum;

    protected virtual TipBox CreateTipBox()
    {
        TextField content = null;
        var title = new TextField(Name, 16f / 23, Color.Gold,
                                        new Rectangle(0, 6, 150, 20))
        {
            Centered = true,
        };
        if (Owner.Name != null && Owner.Name != "")
        {
            content = new TextField(Owner.Name, 15f / 23, Color.White,
                                    new Rectangle(0, 25, 150, 20))
            {
                Centered = true,
            };
        }

        var tip = new TipBox(150, title.RealHeight +
                                    (content != null ? content.RealHeight : 0) + 20);

        tip.Add(title);
        if (content != null)
        {
            tip.Add(content);
        }

        return tip;
    }

    protected virtual void DrawFogOfWar()
    {
        World.FogOfWar.DrawVisibleArea(ViewDistance, Position.X, Position.Y);
    }

    /// <summary>
    /// Whether this game object has been spotted by local player.
    /// </summary>
    public bool Spotted;

    /// <summary>
    /// Copyed model for drawing in the fog of war.
    /// </summary>
    public GameModel modelShadow;

    protected virtual void LeaveFogOfWar()
    {
        modelShadow = null;
        Spotted = true;
    }

    protected virtual void EnterFogOfWar()
    {
        // Create a model shadow, draw the shadow model when we're in fog of war
        if (VisibleInFogOfWar)
        {
            modelShadow = GameModel.ShadowCopy();
            modelShadow.Tint *= 0.3f;
        }
    }

    public GameModel GetAttachment(string boneName)
    {
        var bone = GameModel.GetBone(boneName);

        if (bone < 0)
        {
            return null;
        }

        foreach (KeyValuePair<GameModel, int> pair in Attachment)
        {
            if (pair.Value == bone)
            {
                return pair.Key;
            }
        }

        return null;
    }

    public static float ComputeHit(GameObject from, GameObject to)
    {
        // A * (  K + clamp(1 - D / A) * (1 - K) ) * 10
        const float Scaler = 1.0f;
        const float MaxWeakeness = 0.4f;
        const float BuildingWeakeness = 0.5f;

        var attack = Helper.RandomInRange(from.AttackPoint.X, from.AttackPoint.Y);
        var defense = Helper.RandomInRange(to.DefensePoint.X, to.DefensePoint.Y);

        if (attack < 0)
        {
            attack = 0;
        }

        if (defense < 0)
        {
            defense = 0;
        }

        var value = attack * (MaxWeakeness +
            MathHelper.Clamp(1 - defense / attack, 0, 1) * (1 - MaxWeakeness)) * Scaler;

        return (to is Building) ? value * BuildingWeakeness : value;
    }

    protected virtual void OnSelect(GameUI ui)
    {
        Flash();
    }

    protected virtual void OnDeselect(GameUI ui)
    {
    }

    protected virtual void ShowSpells(GameUI ui)
    {
        ui.ClearUIElement();

        if (Owner is LocalPlayer)
        {
            for (var i = 0; i < SpellList.Count; i++)
            {
                ui.SetUIElement(i, true, SpellList[i].Button);
            }
        }
    }

    protected virtual void OnDie() { }

    /// <summary>
    /// Called after gameworld/UI are initialized.
    /// </summary>
    public virtual void Start(GameWorld world) { }

    /// <summary>
    /// Press the attack.
    /// </summary>
    public virtual void TriggerAttack(Entity target) { }
}

public class Tree : GameObject
{
    public static bool Pickable;

    /// <summary>
    /// Gets or sets how many wood the tree can provide.
    /// </summary>
    public int Lumber
    {
        get => lumber;

        set
        {
            if (everGreen && value < 100)
            {
                value = 100;
            }

            lumber = value;
        }
    }

    private int lumber = 50;
    private bool everGreen;

    /// <summary>
    /// Gets or sets how many peons are harvesting this tree.
    /// </summary>
    public int HarvesterCount { get; set; }

    private Vector3 rotationAxis;
    private float shakeTime;
    private float totalShakeTime = 0.2f;
    private float maxShakeAngle;
    private Quaternion treeRotation;
    private readonly Random random = new();
    private List<Point> pathGrids = new();

    public Tree()
    {
        ShowStatus = false;
        Spotted = true;
    }

    public override void OnDeserialized()
    {
        base.OnDeserialized();

        // Randomize scale and rotation
        var size = Helper.RandomInRange(0.9f, 1.1f);
        Scale = new Vector3(size, size, Helper.RandomInRange(0.9f, 1.1f));
        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, Helper.RandomInRange(0, 2 * MathHelper.Pi));
    }

    public override bool IsPickable => Pickable && base.IsPickable;

    public override bool Intersects(BoundingFrustum frustum)
    {
        return Pickable && base.Intersects(frustum);
    }

    public void Hit(BaseEntity hitter)
    {
        if (lumber > 0 && hitter != null && shakeTime <= 0)
        {
            var toSender = Vector3.Subtract(hitter.Position, Position);
            toSender.Normalize();
            rotationAxis = Vector3.Cross(toSender, Vector3.UnitZ);
            shakeTime = totalShakeTime;
            maxShakeAngle = MathHelper.ToRadians(5 + (float)(random.NextDouble() * 5));
        }

        if (lumber <= 0 && pathGrids != null)
        {
            maxShakeAngle = MathHelper.ToRadians(240);
            shakeTime = totalShakeTime = 2.0f;

            var o = hitter as GameObject;
            if (o != null && o.Owner != null)
            {
                o.Owner.TreesCuttedDown++;
            }

            Audios.Play("TreeFall", o);

            // Remove this obstacle
            World.PathManager.Unmark(pathGrids);
            pathGrids = null;
        }
    }

    public override void OnCreate()
    {
        treeRotation = Rotation;

        pathGrids.AddRange(World.PathManager.EnumerateGridsInCircle(
                           new Vector2(Position.X, Position.Y), 4));
        World.PathManager.Mark(pathGrids);
    }

    private ParticleEffect glow;
    private ParticleEffect star;

    public override void Update(GameTime gameTime)
    {
        const float EffectRadius = 50;
        everGreen = World.GetNearbyObjects(Position, EffectRadius).OfType<Building>().Where(o => o.ClassID == "Lumbermill" && o.Owner.IsAvailable("LiveOfNature")).Any();

        if (everGreen && ShouldDrawModel)
        {
            if (star == null)
            {
                star = new EffectStar(this);

                if (Helper.Random.Next(10) == 0)
                {
                    glow = new EffectGlow(this);
                }
            }

            if (glow != null)
            {
                glow.Update(gameTime);
            }

            if (star != null)
            {
                star.Update(gameTime);
            }
        }

        if (shakeTime > 0)
        {
            var shakeAmount = maxShakeAngle * (float)Math.Sin(
                shakeTime * MathHelper.Pi / totalShakeTime);
            shakeTime -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (shakeAmount > MathHelper.ToRadians(160))
            {
                World.Remove(this);
            }

            Rotation = treeRotation * Quaternion.CreateFromAxisAngle(rotationAxis, shakeAmount);
        }

        base.Update(gameTime);
    }
}

public class Goldmine : GameObject
{
    /// <summary>
    /// Gets or sets how many peons are harvesting this tree.
    /// </summary>
    public int HarvesterCount { get; set; }

    /// <summary>
    /// Gets or sets how much gold the goldmine have.
    /// </summary>
    public int Gold
    {
        get => gold;

        set
        {
            if (value <= 0)
            {
                value = 0;

                // Collapse
                World.Remove(this);
            }

            gold = value;
        }
    }

    private int gold = 10000;
    public Vector2 ObstructorSize { get; set; }
    private readonly List<Point> pathGrids = new();

    public float RotationZ;

    /// <summary>
    /// Gets or sets the spawn point for the goldmine.
    /// </summary>
    public Vector2 SpawnPoint { get; set; }

    public Goldmine()
    {
        Spotted = true;
        AreaRadius = 30;
    }

    public void SetRotation(float value)
    {
        RotationZ = MathHelper.ToRadians(value);
        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RotationZ);
    }

    public override void OnCreate()
    {
        SpawnPoint = Math2D.LocalToWorld(SpawnPoint, Vector2.Zero, RotationZ);

        pathGrids.AddRange(World.PathManager.EnumerateGridsInOutline(Outline));
        World.PathManager.Mark(pathGrids);
    }

    public override void OnDestroy()
    {
        World.PathManager.Unmark(pathGrids);
    }

    protected override void UpdateOutline(Outline outline)
    {
        Vector2 position;
        position.X = Position.X;
        position.Y = Position.Y;

        outline.SetRectangle(-ObstructorSize / 2, ObstructorSize / 2, position, RotationZ);
    }
}

public class BoxOfPandora : GameObject
{
    public BoxOfPandora()
    {
        VisibleInFogOfWar = false;
    }

    public override void OnCreate()
    {
        // Make sure the position is valid
        Vector2 position;

        position.X = Position.X;
        position.Y = Position.Y;
        position = World.PathManager.FindValidPosition(position, null);

        Position = new Vector3(position, 0);

        Update(new GameTime());

        base.OnCreate();
    }

    public override void Update(GameTime gameTime)
    {
        // Checks if anyone hits me
        foreach (var wo in World.GetNearbyObjects(Position, 20))
        {
            if (wo is Charactor o && o.Owner != null &&
                o.Outline.DistanceTo(Outline.Position) < 5)
            {
                // Open the box of pandora
                if (Helper.Random.Next(4) == 0)
                {
                    // Sudden death
                    o.Health -= 200;
                    if (o.Visible && o.Owner is LocalPlayer)
                    {
                        GameUI.Singleton.ShowMessage("-200",
                            TopCenter, MessageType.None, MessageStyle.BubbleUp, Color.Red);
                        Audios.Play("Badluck");
                    }
                }
                else if (Helper.Random.Next(2) == 0)
                {
                    o.Owner.Lumber += 100;
                    if (o.Visible && o.Owner is LocalPlayer)
                    {
                        GameUI.Singleton.ShowMessage("+100",
                            TopCenter, MessageType.None, MessageStyle.BubbleUp, Color.Green);
                        Audios.Play("Treasure");
                    }
                }
                else
                {
                    o.Owner.Gold += 100;
                    if (o.Visible && o.Owner is LocalPlayer)
                    {
                        GameUI.Singleton.ShowMessage("+100",
                            TopCenter, MessageType.None, MessageStyle.BubbleUp, Color.Gold);
                        Audios.Play("Treasure");
                    }
                }

                World.Remove(this);
                return;
            }
        }

        base.Update(gameTime);
    }
}

public interface IProjectile
{
    event EventHandler Hit;

    Entity Target { get; }
}

public class Missile : Entity, IProjectile
{
    public event EventHandler Hit;

    public Entity Target { get; }

    private Vector3 velocity;
    private readonly float scaling;

    public float MaxSpeed = 100;
    public float MaxForce = 500;
    public float Mass = 0.5f;

    public Missile(GameModel ammo, Entity target)
    {
        Target = target;

        GameModel = ammo.ShadowCopy();

        // Compute position
        Position = ammo.Transform.Translation;

        // Compute scaling
        Matrix mx = ammo.Transform;
        mx.Translation = Vector3.Zero;
        var unitY = Vector3.Transform(Vector3.UnitY, mx);
        scaling = unitY.Length();

        // Compute speed
        // velocity = Vector3.Normalize(unitY);
        // velocity *= MaxSpeed;
        velocity = GetTargetPosition() - Position;
        velocity.Z = 0;
        velocity.Z = (float)(velocity.Length() * Math.Tan(MathHelper.ToRadians(30)));
        velocity.Normalize();
        velocity *= MaxSpeed;
    }

    public override void Update(GameTime gameTime)
    {
        Vector3 destination = GetTargetPosition();

        // Creates a force that steer the emitter towards the target position
        Vector3 desiredVelocity = destination - Position;

        desiredVelocity.Normalize();
        desiredVelocity *= MaxSpeed;

        Vector3 force = desiredVelocity - velocity;

        if (force.Length() > MaxForce)
        {
            force.Normalize();
            force *= MaxForce;
        }

        // Update velocity & position
        var elapsedSecond = (float)gameTime.ElapsedGameTime.TotalSeconds;

        velocity += elapsedSecond / Mass * force;
        Position += elapsedSecond * velocity;

        // Hit test
        Vector2 toTarget, facing;

        toTarget.X = destination.X - Position.X;
        toTarget.Y = destination.Y - Position.Y;

        facing.X = velocity.X;
        facing.Y = velocity.Y;

        if (Vector2.Dot(toTarget, facing) <= 0)
        {
            Hit?.Invoke(this, null);

            World.Remove(this);
        }

        // Update ammo transform
        var normalizedVelocity = Vector3.Normalize(velocity);
        var rotationAxis = Vector3.Cross(Vector3.UnitZ, normalizedVelocity);
        var angle = (float)Math.Acos(Vector3.Dot(normalizedVelocity, Vector3.UnitZ));

        var rotation = Matrix.CreateFromAxisAngle(rotationAxis, angle);
        var translation = Matrix.CreateTranslation(Position);

        if (GameModel != null)
        {
            GameModel.Transform = Matrix.CreateScale(scaling) * rotation * translation;
        }
    }

    private Vector3 GetTargetPosition()
    {
        Vector3 destination;

        destination.X = Target.Position.X;
        destination.Y = Target.Position.Y;
        destination.Z = (Target.BoundingBox.Max.Z + Target.Position.Z) / 2;
        return destination;
    }
}

public class Decoration : Entity
{
}
