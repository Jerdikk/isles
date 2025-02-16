// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Isles.Graphics;

public class GameModel
{
    private readonly BaseGame game = BaseGame.Singleton;

    /// <summary>
    /// Gets or sets model world transform.
    /// </summary>
    public Matrix Transform
    {
        get => transform;

        set
        {
            transform = value;
            isBoundingBoxDirty = true;
        }
    }

    private Matrix transform = Matrix.Identity;

    private Matrix[] absoluteNodeTransforms;
    private Matrix[] absoluteBoneTransforms;

    /// <summary>
    /// Gets the axis aligned bounding box of this model.
    /// </summary>
    public BoundingBox BoundingBox
    {
        get
        {
            if (isBoundingBoxDirty)
            {
                boundingBox = AABBFromOBB(localBoundingBox, transform);
                isBoundingBoxDirty = false;
            }

            return boundingBox;
        }
    }

    /// <summary>
    /// Model axis aligned bounding box.
    /// </summary>
    private BoundingBox boundingBox;

    /// <summary>
    /// Bounding box of the xna model. (Not always axis aligned).
    /// </summary>
    private BoundingBox localBoundingBox;

    /// <summary>
    /// Whether we should refresh our axis aligned bounding box.
    /// </summary>
    private bool isBoundingBoxDirty = true;

    /// <summary>
    /// Gets whether a model contains any animation.
    /// </summary>
    private bool IsAnimated => Player != null;

    /// <summary>
    /// Skinned mesh animation player.
    /// We use 2 players to blend between animations.
    /// </summary>
    private AnimationPlayer[] players = new AnimationPlayer[2];

    /// <summary>
    /// Gets the animation player for this game model.
    /// </summary>
    public AnimationPlayer Player => players[currentPlayer];

    /// <summary>
    /// Points to the primary player.
    /// </summary>
    private int currentPlayer;

    /// <summary>
    /// Whether we are blending between two animations.
    /// </summary>
    private bool blending;

    /// <summary>
    /// Time value for animation blending.
    /// </summary>
    private double blendStart;

    /// <summary>
    /// Time value for animation blending.
    /// </summary>
    private double blendDuration;

    /// <summary>
    /// Current animation clip being played.
    /// </summary>
    private string currentClip;

    /// <summary>
    /// Represent the state of current animation.
    /// </summary>
    private enum AnimationState
    {
        Stopped,
        Playing,
        Paused,
    }

    private AnimationState animationState = AnimationState.Stopped;

    private Model model;

    public Vector3 Tint { get; set; } = new(1, 1, 1);

    public Vector3 Glow { get; set; }

    public float Alpha { get; set; } = 1;

    private GameModel()
    {
    }

    public GameModel(string modelName)
    {
        // Make sure this is the upper case Model
        model = game.ModelLoader.LoadModel($"data/{modelName}.gltf");

        if (model.Meshes[0].Joints != null)
        {
            absoluteBoneTransforms = new Matrix[model.Meshes[0].Joints.Length];
        }

        if (model.Animations != null && model.Animations.Count > 0)
        {
            players[0] = new AnimationPlayer(model);
            players[1] = new AnimationPlayer(model);

            UpdateBoneTransform(new GameTime());

            // Play the first animation clip
            Player.Loop = true;
            Play();
        }

        absoluteNodeTransforms = GetAbsoluteNodeTransforms(model);

        // Compute model bounding box.
        localBoundingBox = CalculateBoundingBox(model, absoluteNodeTransforms);
    }

    /// <summary>
    /// Creates a shadow copy of this game model.
    /// The new model refers to the same xna model. Transformation, animation data are copied.
    /// </summary>
    public GameModel ShadowCopy()
    {
        var copy = new GameModel
        {
            animationState = animationState,
            blendDuration = blendDuration,
            blending = blending,
            blendStart = blendStart,
            boundingBox = boundingBox,
            currentClip = currentClip,
            currentPlayer = currentPlayer,
            Tint = Tint,
            Glow = Glow,
            Alpha = Alpha,
            isBoundingBoxDirty = isBoundingBoxDirty,
            model = model,
            localBoundingBox = localBoundingBox,
            players = players,
            transform = transform,
        };

        if (absoluteBoneTransforms != null)
        {
            copy.absoluteBoneTransforms = new Matrix[absoluteBoneTransforms.Length];
            absoluteBoneTransforms.CopyTo(copy.absoluteBoneTransforms, 0);
        }

        copy.absoluteNodeTransforms = new Matrix[absoluteNodeTransforms.Length];
        absoluteNodeTransforms.CopyTo(copy.absoluteNodeTransforms, 0);

        return copy;
    }

    /// <summary>
    /// Gets the index of a bone with the specific name.
    /// </summary>
    /// <returns>
    /// Negtive if the bone not found.
    /// </returns>
    public int GetBone(string boneName)
    {
        return model.NodeNames.TryGetValue(boneName, out var node) ? node.Index : -1;
    }

    /// <summary>
    /// Gets the global transformation of the bone.
    /// NOTE: Call this after the model gets drawed.
    /// </summary>
    public Matrix GetBoneTransform(int bone)
    {
        return absoluteNodeTransforms[bone] * transform;
    }

    /// <summary>
    /// Play the current (or default) animation.
    /// </summary>
    /// <returns>Succeeded or not.</returns>
    private bool Play()
    {
        if (!IsAnimated)
        {
            return false;
        }

        // Play current animation if it is paused
        if (animationState == AnimationState.Paused)
        {
            animationState = AnimationState.Playing;
        }
        else if (animationState == AnimationState.Stopped)
        {
            players[currentPlayer].StartClip(currentClip);
            animationState = AnimationState.Playing;
        }

        return true;
    }

    public bool Play(string clip)
    {
        if (!IsAnimated || clip == null)
        {
            return false;
        }

        // Do nothing if it's still the same animation clip
        if (clip == currentClip)
        {
            return true;
        }

        currentClip = clip;
        Player.Triggers = null;
        Player.Loop = true;
        Player.Complete = null;
        Player.StartClip(clip);
        animationState = AnimationState.Playing;
        return true;
    }

    public bool Play(
        string clip, bool loop, float blendTime,
        EventHandler OnComplete = null,
        params (float percent, Action)[] triggers)
    {
        if (!IsAnimated || clip == null)
        {
            return false;
        }

        // Do nothing if it's still the same animation clip
        if (clip == currentClip)
        {
            return true;
        }

        // No blend occurs if there's no blend duration
        if (blendTime > 0)
        {
            // Start the new animation clip on the other player
            currentPlayer = 1 - currentPlayer;
            blending = true;
            blendStart = game.CurrentGameTime.TotalGameTime.TotalSeconds;
            blendDuration = blendTime;
        }

        Player.StartClip(clip);
        Player.Loop = loop;
        Player.Complete = OnComplete;
        Player.Triggers = triggers;
        animationState = AnimationState.Playing;
        currentClip = clip;
        return true;
    }

    public void Pause()
    {
        if (animationState == AnimationState.Playing)
        {
            animationState = AnimationState.Paused;
        }
    }

    public virtual void Update(GameTime gameTime)
    {
        if (!IsAnimated)
        {
            return;
        }

        // Apply animation speed
        TimeSpan time = (animationState != AnimationState.Playing) ? TimeSpan.Zero : gameTime.ElapsedGameTime;

        players[currentPlayer].Update(time, true);

        // update both players when we are blending animations
        if (blending)
        {
            players[1 - currentPlayer].Update(time, true);
        }

        UpdateBoneTransform(gameTime);
    }

    private void UpdateBoneTransform(GameTime gameTime)
    {
        // Update skin transforms (stored in bones)
        absoluteNodeTransforms = players[currentPlayer].GetWorldTransforms();

        // Lerp transforms when we are blending between animations
        if (blending)
        {
            // End blend if time exceeds
            var timeNow = gameTime.TotalGameTime.TotalSeconds;

            if (timeNow - blendStart > blendDuration)
            {
                blending = false;
            }

            // Compute lerp amount
            var amount = (float)((timeNow - blendStart) / blendDuration);

            // Clamp lerp amount to [0..1]
            amount = MathHelper.Clamp(amount, 0.0f, 1.0f);

            // Get old transforms
            Matrix[] prevBoneTransforms = players[1 - currentPlayer].GetWorldTransforms();

            // Perform matrix lerp on all skin transforms
            for (var i = 0; i < absoluteNodeTransforms.Length; i++)
            {
                absoluteNodeTransforms[i] = Matrix.Lerp(prevBoneTransforms[i], absoluteNodeTransforms[i], amount);
            }
        }

        if (model.Meshes[0].Joints != null)
        {
            var mesh = model.Meshes[0];
            for (var i = 0; i < mesh.Joints.Length; i++)
            {
                absoluteBoneTransforms[i] = mesh.InverseBindMatrices[i] * absoluteNodeTransforms[mesh.Joints[i].Index];
            }
        }
    }

    public void Draw(Vector4? color = null)
    {
        var tint = color ?? new Vector4(Tint, MathHelper.Clamp(Alpha, 0, 1));
        var glow = new Vector4(Glow, 1);

        foreach (var mesh in model.Meshes)
        {
            if (mesh.Joints != null)
            {
                game.ModelRenderer.AddDrawable(mesh, transform, absoluteBoneTransforms, tint, glow);
            }
            else
            {
                game.ModelRenderer.AddDrawable(mesh, absoluteNodeTransforms[mesh.Node.Index] * transform, null, tint, glow);
            }
        }
    }

    private static BoundingBox CalculateBoundingBox(Model model, Matrix[] absoluteNodeTransforms)
    {
        var min = Vector3.One * float.MaxValue;
        var max = Vector3.One * float.MinValue;

        foreach (var mesh in model.Meshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                foreach (var corner in primitive.BoundingBox.GetCorners())
                {
                    // Transform vertex
                    var v = Vector3.Transform(corner, absoluteNodeTransforms[mesh.Node.Index]);

                    if (v.X < min.X)
                    {
                        min.X = v.X;
                    }

                    if (v.X > max.X)
                    {
                        max.X = v.X;
                    }

                    if (v.Y < min.Y)
                    {
                        min.Y = v.Y;
                    }

                    if (v.Y > max.Y)
                    {
                        max.Y = v.Y;
                    }

                    if (v.Z < min.Z)
                    {
                        min.Z = v.Z;
                    }

                    if (v.Z > max.Z)
                    {
                        max.Z = v.Z;
                    }
                }
            }
        }

        return new BoundingBox(min, max);
    }

    private static Matrix[] GetAbsoluteNodeTransforms(Model model)
    {
        var result = new Matrix[model.Nodes.Length];

        foreach (var node in model.Nodes)
        {
            result[node.Index] = Matrix.CreateScale(node.Scale) *
                                 Matrix.CreateFromQuaternion(node.Rotation) *
                                 Matrix.CreateTranslation(node.Translation);

            if (node.ParentIndex >= 0)
            {
                result[node.Index] *= result[node.ParentIndex];
            }
        }

        return result;
    }

    /// <summary>
    /// Compute the axis aligned bounding box from an oriented bounding box.
    /// </summary>
    private static BoundingBox AABBFromOBB(BoundingBox box, Matrix transform)
    {
        const float FloatMax = 1000000;

        // Find the 8 corners
        Vector3[] corners = box.GetCorners();

        // Compute bounding box
        var min = new Vector3(FloatMax, FloatMax, FloatMax);
        var max = new Vector3(-FloatMax, -FloatMax, -FloatMax);

        foreach (Vector3 c in corners)
        {
            var v = Vector3.Transform(c, transform);

            if (v.X < min.X)
            {
                min.X = v.X;
            }

            if (v.X > max.X)
            {
                max.X = v.X;
            }

            if (v.Y < min.Y)
            {
                min.Y = v.Y;
            }

            if (v.Y > max.Y)
            {
                max.Y = v.Y;
            }

            if (v.Z < min.Z)
            {
                min.Z = v.Z;
            }

            if (v.Z > max.Z)
            {
                max.Z = v.Z;
            }
        }

        return new BoundingBox(min, max);
    }
}
