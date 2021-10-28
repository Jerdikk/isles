// Copyright (c) Yufei Huang. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Isles.Engine;
using Isles.Pipeline;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Isles.Graphics
{
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

        private Matrix[] bones;
        private Matrix[] skinTransforms;

        /// <summary>
        /// Gets the axis aligned bounding box of this model.
        /// </summary>
        public BoundingBox BoundingBox
        {
            get
            {
                if (isBoundingBoxDirty)
                {
                    boundingBox = AABBFromOBB(orientedBoundingBox, transform);
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
        private BoundingBox orientedBoundingBox;

        /// <summary>
        /// Whether we should refresh our axis aligned bounding box.
        /// </summary>
        private bool isBoundingBoxDirty = true;

        /// <summary>
        /// Gets whether a model contains any animation.
        /// </summary>
        private bool IsAnimated => Player != null;

        /// <summary>
        /// Gets whether the model is a skinned model.
        /// </summary>
        private bool IsSkinned => skin != null;

        /// <summary>
        /// Skinned mesh animation player.
        /// We use 2 players to blend between animations.
        /// </summary>
        private AnimationPlayer[] players = new AnimationPlayer[2];

        /// <summary>
        /// Gets the animation player for this game model.
        /// </summary>
        public AnimationPlayer Player => players[currentPlayer];

        private IDictionary<string, AnimationClip> animationClips;

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
        private AnimationClip currentClip;

        /// <summary>
        /// Represent the state of current animation.
        /// </summary>
        private enum AnimationState
        {
            Stopped,
            Playing,
            Paused,
        }

        /// <summary>
        /// Current animation state.
        /// </summary>
        private AnimationState animationState = AnimationState.Stopped;

        /// <summary>
        /// For skinned mesh.
        /// </summary>
        private SkinningData skin;

        /// <summary>
        /// Space partition information for this model.
        /// </summary>
        private ModelSpacePartitionInformation spacePartitionInfo;

        private Model model;
        private GltfModel gltfModel;

        public Vector3 Tint { get; set; } = new(1, 1, 1);

        public Vector3 Glow { get; set; }

        public float Alpha { get; set; } = 1;

        private GameModel()
        {
        }

        public GameModel(string modelName)
        {
            // Make sure this is the upper case Model
            model = game.Content.Load<Model>(modelName);
            gltfModel = game.ModelLoader.LoadModel($"data/{modelName}.gltf");
            Init();
        }

        /// <summary>
        /// Creates a shadow copy of this game model.
        /// The new model refers to the same xna model. Transformation, animation data are copied.
        /// </summary>
        public GameModel ShadowCopy()
        {
            var copy = new GameModel
            {
                animationClips = animationClips,
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
                gltfModel = gltfModel,
                orientedBoundingBox = orientedBoundingBox,
                players = players,
                skin = skin,
                spacePartitionInfo = spacePartitionInfo,
                transform = transform,
            };

            if (skinTransforms != null)
            {
                copy.skinTransforms = new Matrix[skinTransforms.Length];
                skinTransforms.CopyTo(copy.skinTransforms, 0);
            }

            copy.bones = new Matrix[bones.Length];
            bones.CopyTo(copy.bones, 0);

            return copy;
        }

        private void Init()
        {
            if (model.Tag is Dictionary<string, object> dictionary)
            {
                if (dictionary.TryGetValue("SkinningData", out var value))
                {
                    skin = value as SkinningData;
                }

                if (dictionary.TryGetValue("AnimationData", out value))
                {
                    animationClips = value as IDictionary<string, AnimationClip>;
                }

                dictionary.TryGetValue("SpacePartition", out value);

                spacePartitionInfo = new ModelSpacePartitionInformation
                {
                    BitMap = value as bool[],
                    Box = OBBFromModel(model),
                };
            }

            if (animationClips != null)
            {
                if (IsSkinned)
                {
                    players[0] = new AnimationPlayer(skin);
                    players[1] = new AnimationPlayer(skin);

                    UpdateSkinTransform(new GameTime());
                }
                else
                {
                    players[0] = new AnimationPlayer(model);
                    players[1] = new AnimationPlayer(model);

                    UpdateBoneTransform(new GameTime());
                }

                // Play the first animation clip
                Player.Loop = true;
                Play();
            }

            // Adjust bone array size
            bones = new Matrix[gltfModel.Nodes.Length];
            model.CopyAbsoluteBoneTransformsTo(bones);

            // Compute model bounding box.
            orientedBoundingBox = OBBFromModel(model);
        }

        /// <summary>
        /// Gets the index of a bone with the specific name.
        /// </summary>
        /// <returns>
        /// Negtive if the bone not found.
        /// </returns>
        public int GetBone(string boneName)
        {
            if (IsSkinned)
            {
                for (var i = 0; i < skin.BoneName.Count; i++)
                {
                    if (skin.BoneName[i].Equals(boneName))
                    {
                        return i;
                    }
                }
            }
            else
            {
                if (model.Bones.TryGetValue(boneName, out ModelBone bone))
                {
                    return bone.Index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the global transformation of the bone.
        /// NOTE: Call this after the model gets drawed.
        /// </summary>
        public Matrix GetBoneTransform(int bone)
        {
            return bones[bone];
        }

        /// <summary>
        /// Ray intersection test.
        /// </summary>
        /// <param name="ray">Target ray.</param>
        /// <returns>
        /// Distance from the intersection point to the ray starting position,
        /// Null if there's no intersection.
        /// </returns>
        public float? Intersects(Ray ray)
        {
            if (spacePartitionInfo == null)
            {
                return BoundingBox.Intersects(ray);
            }

            var m = Matrix.Invert(transform);
            var relarayEnd = Vector3.Transform(ray.Position + ray.Direction, m);
            var relarayPosition = Vector3.Transform(ray.Position, m);
            var relaray = new Ray(relarayPosition, relarayEnd - relarayPosition);
            return Intersects(relaray, 0, 0);
        }

        private float? Intersects(Ray ray, int level, int index)
        {
            BoundingBox box;
            float? dist = null;
            if (level == 3)
            {
                if (spacePartitionInfo.BitMap[index])
                {
                    box = spacePartitionInfo.IndexToBoundingBox(index);
                    dist = ray.Intersects(box);
                }

                return dist;
            }

            var minLength = float.PositiveInfinity;
            if (!spacePartitionInfo.BitMap[index])
            {
                dist = ray.Intersects(spacePartitionInfo.IndexToBoundingBox(index));
                if (!dist.HasValue)
                {
                    return dist;
                }
            }

            for (var i = 0; i < 8; i++)
            {
                var son = spacePartitionInfo.SonOf(index, i, level);
                if (spacePartitionInfo.BitMap[son])
                {
                    dist = ray.Intersects(spacePartitionInfo.IndexToBoundingBox(son));
                    if (dist.HasValue)
                    {
                        var d = Intersects(ray, level + 1, son);
                        if (d.HasValue)
                        {
                            minLength = Math.Min(d.Value, minLength);
                        }
                    }
                }
            }

            dist = minLength != float.PositiveInfinity ? minLength : (float?)null;
            return dist;
        }

        /// <summary>
        /// Gets the default (first) animation clip of this game model.
        /// </summary>
        public AnimationClip GetDefaultAnimationClip()
        {
            if (!IsAnimated)
            {
                return null;
            }

            // This is a little bit tricky
            IEnumerator<KeyValuePair<string, AnimationClip>> enumerator =
                animationClips.GetEnumerator();

            return enumerator.MoveNext() ? enumerator.Current.Value : null;
        }

        /// <summary>
        /// Gets the animation clip with the specified name.
        /// </summary>
        public AnimationClip GetAnimationClip(string clipName)
        {
            return animationClips.TryGetValue(clipName, out AnimationClip value) ? value : null;
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

            if (currentClip == null)
            {
                currentClip = GetDefaultAnimationClip();
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

        /// <summary>
        /// Play an animation clip.
        /// </summary>
        /// <param name="clipName">
        /// Clip name.
        /// </param>
        /// <returns>Succeeded or not.</returns>
        public bool Play(string clipName)
        {
            if (!IsAnimated || clipName == null)
            {
                return false;
            }

            // Play the animation clip with the specified name
            if (animationClips.TryGetValue(clipName, out AnimationClip clip))
            {
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

            // Stop if the clip is invalid
            Stop();
            return false;
        }

        public bool Play(string clipName, bool loop, float blendTime)
        {
            return Play(clipName, loop, blendTime, null, null);
        }

        /// <summary>
        /// Play an animation clip. Blend the new animation with the
        /// old one to provide smooth transition.
        /// </summary>
        /// <param name="clipName"></param>
        /// <param name="blendTime"></param>
        /// <returns>Succeeded or not.</returns>
        public bool Play(string clipName, bool loop, float blendTime, EventHandler OnComplete,
                         IEnumerable<KeyValuePair<TimeSpan, EventHandler>> triggers)
        {
            if (!IsAnimated || clipName == null)
            {
                return false;
            }

            // Play the animation clip with the specified name
            if (animationClips.TryGetValue(clipName, out AnimationClip clip))
            {
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

            // Stop if the clip is invalid
            Stop();
            return false;
        }

        /// <summary>
        /// Pause the current animation.
        /// </summary>
        public void Pause()
        {
            if (animationState == AnimationState.Playing)
            {
                animationState = AnimationState.Paused;
            }
        }

        /// <summary>
        /// Stop the current animation (Reset current animation to the first frame).
        /// </summary>
        public void Stop()
        {
            if (IsAnimated)
            {
                // Reset current animation
                players[currentPlayer].Update(TimeSpan.Zero, false);

                // Change animation state
                animationState = AnimationState.Stopped;

                // Stop blending for whatever reason
                blending = false;
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

            if (IsSkinned)
            {
                UpdateSkinTransform(gameTime);
            }
            else
            {
                UpdateBoneTransform(gameTime);
            }
        }

        private void UpdateBoneTransform(GameTime gameTime)
        {
            // Update skin transforms (stored in bones)
            bones = players[currentPlayer].GetWorldTransforms();

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
                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i] = Matrix.Lerp(prevBoneTransforms[i], bones[i], amount);
                }
            }
        }

        private void UpdateSkinTransform(GameTime gameTime)
        {
            // Update skin transforms (stored in bones)
            bones = players[currentPlayer].GetWorldTransforms();
            skinTransforms = players[currentPlayer].GetSkinTransforms();

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
                Matrix[] prevWorldTransforms = players[1 - currentPlayer].GetWorldTransforms();
                Matrix[] prevSkinTransforms = players[1 - currentPlayer].GetSkinTransforms();

                // Perform matrix lerp on all skin transforms
                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i] = Matrix.Lerp(prevWorldTransforms[i], bones[i], amount);
                    skinTransforms[i] = Matrix.Lerp(prevSkinTransforms[i], skinTransforms[i], amount);
                }
            }
        }

        public void Draw()
        {
            var tint = new Vector4(Tint.X, Tint.Y, Tint.Z, MathHelper.Clamp(Alpha, 0, 1));
            var glow = new Vector4(Glow.X, Glow.Y, Glow.Z, 1);

            foreach (var mesh in gltfModel.Meshes)
            {
                if (IsSkinned)
                {
                    game.ModelRenderer.Draw(mesh, transform, skinTransforms, tint, glow);
                }
                else
                {
                    game.ModelRenderer.Draw(mesh, bones[mesh.Node.Index] * transform, null, tint, glow);
                }
            }
        }

        /// <summary>
        /// Compute bounding box for the specified xna model.
        /// </summary>
        protected static BoundingBox OBBFromModel(Model model)
        {
            if (null == model || model.Meshes.Count <= 0)
            {
                return new BoundingBox(Vector3.Zero, Vector3.Zero);
            }

            const float FloatMax = 1000000;

            // Compute bounding box
            var min = new Vector3(FloatMax, FloatMax, FloatMax);
            var max = new Vector3(-FloatMax, -FloatMax, -FloatMax);

            var bones = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(bones);

            foreach (ModelMesh mesh in model.Meshes)
            {
                var stride = mesh.MeshParts[0].VertexStride;
                var elementCount = mesh.VertexBuffer.SizeInBytes / stride;
                var vertices = new Vector3[elementCount];
                mesh.VertexBuffer.GetData(0, vertices, 0, elementCount, stride);

                foreach (Vector3 vertex in vertices)
                {
                    // Transform vertex
                    var v = Vector3.Transform(vertex, bones[mesh.ParentBone.Index]);

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

            return new BoundingBox(min, max);
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

    /// <summary>
    /// The animation player is in charge of decoding bone position
    /// matrices from an animation clip.
    /// </summary>
    public class AnimationPlayer
    {
        private readonly Model model;
        private TimeSpan currentTimeValue;
        private int currentKeyframe;

        // Current animation transform matrices.
        private readonly Matrix[] boneTransforms;
        private readonly Matrix[] worldTransforms;
        private readonly Matrix[] skinTransforms;

        // Backlink to the bind pose and skeleton hierarchy data.
        private readonly SkinningData skinningDataValue;

        public IEnumerable<KeyValuePair<TimeSpan, EventHandler>> Triggers;
        public EventHandler Complete;
        public bool Loop;

        /// <summary>
        /// Constructs a new animation player.
        /// </summary>
        public AnimationPlayer(SkinningData skinningData)
        {
            skinningDataValue = skinningData ?? throw new ArgumentNullException();

            boneTransforms = new Matrix[skinningData.BindPose.Count];
            worldTransforms = new Matrix[skinningData.BindPose.Count];
            skinTransforms = new Matrix[skinningData.BindPose.Count];
        }

        public AnimationPlayer(Model model)
        {
            this.model = model ?? throw new ArgumentNullException();
            boneTransforms = new Matrix[model.Bones.Count];
            worldTransforms = new Matrix[model.Bones.Count];
        }

        /// <summary>
        /// Starts decoding the specified animation clip.
        /// </summary>
        public void StartClip(AnimationClip clip)
        {
            CurrentClip = clip ?? throw new ArgumentNullException("clip");
            currentTimeValue = TimeSpan.Zero;
            currentKeyframe = 0;

            // Initialize bone transforms to the bind pose.
            if (skinningDataValue != null)
            {
                skinningDataValue.BindPose.CopyTo(boneTransforms, 0);
            }
            else if (model != null)
            {
                model.CopyBoneTransformsTo(boneTransforms);
            }
        }

        /// <summary>
        /// Advances the current animation position.
        /// </summary>
        public void Update(TimeSpan time, bool relativeToCurrentTime)
        {
            UpdateBoneTransforms(time, relativeToCurrentTime);
            UpdateWorldTransforms();

            if (skinningDataValue != null)
            {
                UpdateSkinTransforms();
            }
        }

        /// <summary>
        /// Helper used by the Update method to refresh the BoneTransforms data.
        /// </summary>
        public void UpdateBoneTransforms(TimeSpan time, bool relativeToCurrentTime)
        {
            if (CurrentClip == null)
            {
                throw new InvalidOperationException(
                            "AnimationPlayer.Update was called before StartClip");
            }

            if (currentTimeValue >= CurrentClip.Duration && !Loop)
            {
                return;
            }

            // Update triggers
            if (Triggers != null)
            {
                foreach (KeyValuePair<TimeSpan, EventHandler> trigger in Triggers)
                {
                    if (currentTimeValue < trigger.Key &&
                        currentTimeValue + time > trigger.Key)
                    {
                        trigger.Value(this, null);
                    }
                }
            }

            // Update the animation position.
            if (relativeToCurrentTime)
            {
                time += currentTimeValue;

                // If we reached the end, loop back to the start.
                while (time >= CurrentClip.Duration)
                {
                    // Trigger complete event
                    Complete?.Invoke(null, EventArgs.Empty);

                    if (Loop)
                    {
                        time -= CurrentClip.Duration;
                    }
                    else
                    {
                        currentTimeValue = CurrentClip.Duration;
                        time = currentTimeValue;
                        break;
                    }
                }
            }

            if ((time < TimeSpan.Zero) || (time > CurrentClip.Duration))
            {
                throw new ArgumentOutOfRangeException("time");
            }

            // If the position moved backwards, reset the keyframe index.
            if (time < currentTimeValue)
            {
                currentKeyframe = 0;
                if (skinningDataValue != null)
                {
                    skinningDataValue.BindPose.CopyTo(boneTransforms, 0);
                }
                else if (model != null)
                {
                    model.CopyBoneTransformsTo(boneTransforms);
                }
            }

            currentTimeValue = time;

            // Read keyframe matrices.
            IList<Keyframe> keyframes = CurrentClip.Keyframes;

            while (currentKeyframe < keyframes.Count)
            {
                Keyframe keyframe = keyframes[currentKeyframe];

                // Stop when we've read up to the current time position.
                if (keyframe.Time > currentTimeValue)
                {
                    break;
                }

                // Use this keyframe.
                boneTransforms[keyframe.Bone] = keyframe.Transform;

                currentKeyframe++;
            }
        }

        /// <summary>
        /// Helper used by the Update method to refresh the WorldTransforms data.
        /// </summary>
        public void UpdateWorldTransforms()
        {
            // Root bone.
            worldTransforms[0] = boneTransforms[0];

            // Child bones.
            for (var bone = 1; bone < worldTransforms.Length; bone++)
            {
                var parentBone = -1;

                if (skinningDataValue != null)
                {
                    parentBone = skinningDataValue.SkeletonHierarchy[bone];
                }
                else if (model != null)
                {
                    parentBone = model.Bones[bone].Parent.Index;
                }

                worldTransforms[bone] = boneTransforms[bone] * worldTransforms[parentBone];
            }
        }

        /// <summary>
        /// Helper used by the Update method to refresh the SkinTransforms data.
        /// </summary>
        public void UpdateSkinTransforms()
        {
            for (var bone = 0; bone < skinTransforms.Length; bone++)
            {
                skinTransforms[bone] = skinningDataValue.InverseBindPose[bone] *
                                            worldTransforms[bone];
            }
        }

        /// <summary>
        /// Gets the current bone transform matrices, relative to their parent bones.
        /// </summary>
        public Matrix[] GetBoneTransforms()
        {
            return boneTransforms;
        }

        /// <summary>
        /// Gets the current bone transform matrices, in absolute format.
        /// </summary>
        public Matrix[] GetWorldTransforms()
        {
            return worldTransforms;
        }

        /// <summary>
        /// Gets the current bone transform matrices,
        /// relative to the skinning bind pose.
        /// </summary>
        public Matrix[] GetSkinTransforms()
        {
            return skinTransforms;
        }

        /// <summary>
        /// Gets the clip currently being decoded.
        /// </summary>
        public AnimationClip CurrentClip { get; private set; }

        /// <summary>
        /// Gets the current play position.
        /// </summary>
        public TimeSpan CurrentTime => currentTimeValue;
    }

    /// <summary>
    /// Loads SkinningData objects from compiled XNB format.
    /// </summary>
    public class SkinningDataReader : ContentTypeReader<SkinningData>
    {
        protected override SkinningData Read(ContentReader input,
                                             SkinningData existingInstance)
        {
            IList<Matrix> bindPose, inverseBindPose;
            IList<int> skeletonHierarchy;
            IList<string> boneName;

            bindPose = input.ReadObject<IList<Matrix>>();
            inverseBindPose = input.ReadObject<IList<Matrix>>();
            skeletonHierarchy = input.ReadObject<IList<int>>();
            boneName = input.ReadObject<IList<string>>();

            return new SkinningData(bindPose, inverseBindPose,
                                    skeletonHierarchy, boneName);
        }
    }

    /// <summary>
    /// Loads AnimationClip objects from compiled XNB format.
    /// </summary>
    public class AnimationClipReader : ContentTypeReader<AnimationClip>
    {
        protected override AnimationClip Read(ContentReader input,
                                              AnimationClip existingInstance)
        {
            TimeSpan duration = input.ReadObject<TimeSpan>();
            IList<Keyframe> keyframes = input.ReadObject<IList<Keyframe>>();

            return new AnimationClip(duration, keyframes);
        }
    }

    /// <summary>
    /// Loads Keyframe objects from compiled XNB format.
    /// </summary>
    public class KeyframeReader : ContentTypeReader<Keyframe>
    {
        protected override Keyframe Read(ContentReader input,
                                         Keyframe existingInstance)
        {
            var bone = input.ReadObject<int>();
            TimeSpan time = input.ReadObject<TimeSpan>();
            Matrix transform = input.ReadObject<Matrix>();

            return new Keyframe(bone, time, transform);
        }
    }
}
