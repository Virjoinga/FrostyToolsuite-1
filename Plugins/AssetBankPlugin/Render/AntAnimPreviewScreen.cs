using System;
using AssetBankPlugin.Export;
using MeshSetPlugin.Render;
using MeshSetPlugin.Screens;

namespace AssetBankPlugin.Render
{
    public class AntAnimPreviewScreen : MultiMeshPreviewScreen
    {
        public MeshRenderSkeleton CurrentSkeleton { get; private set; }
        public double SpeedMultiplier { get; set; } = 1.0;
        public bool IsPaused { get; set; } = false;
        public int CurrentFrame { get; private set; }
        public int TotalFrames { get; private set; } = 1;

        public event Action<int> FrameChanged;

        private InternalAnimation _storedAnim;
        private int _storedEndFrame;

        private MeshRenderAnim _currentRenderAnim;

        private double _currentTime = 0;
        private int _trackFrame = 0;
        private const double FrameAdvanceThreshold = 1.0 / 30.0;

        private volatile bool _forceSkeletonUpdate = false;

        public void LoadSkeleton(MeshRenderSkeleton skeleton)
        {
            CurrentSkeleton = skeleton;
            VisualizeSkeleton = skeleton;
        }

        public void LoadAnimation(MeshRenderAnim anim, InternalAnimation internalAnim, int endFrame)
        {
            _storedAnim = internalAnim;
            _storedEndFrame = endFrame;
            _currentRenderAnim = anim;
            TotalFrames = Math.Max(1, endFrame);
            _currentTime = 0;
            _trackFrame = 0;
            CurrentFrame = 0;
            IsPaused = false;
            SetAnimation(anim);
        }

        public void Clear()
        {
            _storedAnim = null;
            _storedEndFrame = 0;
            _currentRenderAnim = null;
            TotalFrames = 1;
            _currentTime = 0;
            _trackFrame = 0;
            CurrentFrame = 0;
            IsPaused = false;
            SetAnimation(null);
            FrameChanged?.Invoke(0);
        }

        public void Restart()
        {
            if (_storedAnim == null) return;
            var fresh = AntAnimationConverter.Convert(_storedAnim, _storedEndFrame);
            _currentRenderAnim = fresh;
            _currentTime = 0;
            _trackFrame = 0;
            CurrentFrame = 0;
            SetAnimation(fresh);
            FrameChanged?.Invoke(0);
        }

        public void RefreshPose()
        {
            if (IsPaused)
                _forceSkeletonUpdate = true;
        }

        public void SeekToFrame(int targetFrame)
        {
            if (_storedAnim == null) return;
            targetFrame = Math.Max(0, targetFrame % TotalFrames);

            var fresh = AntAnimationConverter.Convert(_storedAnim, _storedEndFrame);
            double dt = FrameAdvanceThreshold + 0.001;
            for (int i = 0; i < targetFrame; i++)
                fresh.Update(dt);

            _currentRenderAnim = fresh;
            _currentTime = 0;
            _trackFrame = targetFrame;
            CurrentFrame = targetFrame;
            SetAnimation(fresh);
            _forceSkeletonUpdate = true;
            FrameChanged?.Invoke(targetFrame);
        }

        public override void Update(double timestep)
        {
            if (IsPaused)
            {
                if (_forceSkeletonUpdate)
                {
                    _forceSkeletonUpdate = false;
                    base.Update(0);
                }
                else
                {
                    SetAnimation(null);
                    base.Update(timestep);
                    SetAnimation(_currentRenderAnim);
                }
                return;
            }

            double scaled = timestep * SpeedMultiplier;
            base.Update(scaled);

            _currentTime += scaled;
            if (_currentTime > FrameAdvanceThreshold)
            {
                _currentTime = 0;
                _trackFrame = (_trackFrame + 1) % TotalFrames;
                if (_trackFrame != CurrentFrame)
                {
                    CurrentFrame = _trackFrame;
                    FrameChanged?.Invoke(CurrentFrame);
                }
            }
        }
    }
}