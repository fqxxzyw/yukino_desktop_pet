using System;
using System.Collections.Generic;

namespace YukinoPet
{
    internal sealed class AnimationClip
    {
        public readonly string Name;
        public readonly int Row;
        public readonly int StartColumn;
        public readonly int FrameCount;
        public readonly int[] FrameDurationsMs;
        public readonly bool Loop;
        public readonly int Priority;
        public readonly bool CanInterrupt;
        public readonly int RandomFirstHoldMinMs;
        public readonly int RandomFirstHoldMaxMs;

        public AnimationClip(string name, int row, int startColumn, int frameCount, int[] durations, bool loop, int priority, bool canInterrupt, int randomFirstMin, int randomFirstMax)
        {
            Name = name;
            Row = row;
            StartColumn = startColumn;
            FrameCount = frameCount;
            FrameDurationsMs = durations;
            Loop = loop;
            Priority = priority;
            CanInterrupt = canInterrupt;
            RandomFirstHoldMinMs = randomFirstMin;
            RandomFirstHoldMaxMs = randomFirstMax;
        }

        public int BaseDuration(int frame)
        {
            return FrameDurationsMs.Length == 1 ? FrameDurationsMs[0] : FrameDurationsMs[Math.Min(frame, FrameDurationsMs.Length - 1)];
        }
    }

    internal sealed class AnimationController
    {
        private readonly Dictionary<string, AnimationClip> clips;
        private readonly Random random;
        private AnimationClip current;
        private int frame;
        private double elapsedInFrameMs;
        private double currentFrameDurationMs;
        private Action completion;
        private bool changed = true;

        public AnimationController(Dictionary<string, AnimationClip> clips, Random random)
        {
            this.clips = clips;
            this.random = random;
            Play("idle", true, null);
        }

        public string CurrentName { get { return current.Name; } }
        public int CurrentRow { get { return current.Row; } }
        public int CurrentColumn { get { return current.StartColumn + frame; } }
        public int CurrentPriority { get { return current.Priority; } }
        public bool IsOneShotPlaying { get { return !current.Loop && frame < current.FrameCount; } }

        public bool Play(string name, bool force, Action onComplete)
        {
            AnimationClip next;
            if (!clips.TryGetValue(name, out next)) return false;
            if (!force && current != null && !current.CanInterrupt && next.Priority < current.Priority) return false;
            if (!force && current == next && next.Loop) return true;
            current = next;
            frame = 0;
            elapsedInFrameMs = 0;
            completion = onComplete;
            SampleDuration();
            changed = true;
            return true;
        }

        public bool Update(double elapsedSeconds, bool paused)
        {
            if (paused) return ConsumeChanged();
            elapsedInFrameMs += Math.Max(0, Math.Min(0.12, elapsedSeconds)) * 1000.0;
            int safety = 0;
            while (elapsedInFrameMs >= currentFrameDurationMs && safety++ < 12)
            {
                elapsedInFrameMs -= currentFrameDurationMs;
                frame++;
                if (frame >= current.FrameCount)
                {
                    if (current.Loop) frame = 0;
                    else
                    {
                        Action callback = completion;
                        completion = null;
                        if (callback != null) callback();
                        if (frame >= current.FrameCount) Play("idle", true, null);
                        return ConsumeChanged();
                    }
                }
                SampleDuration();
                changed = true;
            }
            return ConsumeChanged();
        }

        private void SampleDuration()
        {
            if (frame == 0 && current.RandomFirstHoldMaxMs > current.RandomFirstHoldMinMs)
                currentFrameDurationMs = random.Next(current.RandomFirstHoldMinMs, current.RandomFirstHoldMaxMs + 1);
            else currentFrameDurationMs = Math.Max(16, current.BaseDuration(frame));
        }

        private bool ConsumeChanged()
        {
            bool value = changed;
            changed = false;
            return value;
        }
    }

    internal sealed class LookController
    {
        private int currentDirection = -1;
        private int targetDirection = -1;
        private double stepElapsed;
        private double releaseElapsed;

        public bool Active { get { return currentDirection >= 0; } }
        public int Row { get { return currentDirection < 8 ? 9 : 10; } }
        public int Column { get { return currentDirection < 8 ? currentDirection : currentDirection - 8; } }

        public void Aim(double deltaX, double deltaY)
        {
            if (Math.Abs(deltaX) + Math.Abs(deltaY) < 16) return;
            double angle = Math.Atan2(deltaX, -deltaY) * 180.0 / Math.PI;
            if (angle < 0) angle += 360;
            int next = ((int)Math.Round(angle / 22.5)) & 15;
            if (targetDirection >= 0 && CircularDistance(targetDirection, next) == 0) return;
            targetDirection = next;
            releaseElapsed = 0;
            if (currentDirection < 0) currentDirection = next;
        }

        public void Release()
        {
            targetDirection = -1;
        }

        public bool Update(double elapsedSeconds)
        {
            if (currentDirection < 0) return false;
            bool changed = false;
            if (targetDirection < 0)
            {
                releaseElapsed += elapsedSeconds;
                if (releaseElapsed >= 0.24)
                {
                    currentDirection = -1;
                    changed = true;
                }
                return changed;
            }
            releaseElapsed = 0;
            stepElapsed += elapsedSeconds;
            if (stepElapsed < 0.075 || currentDirection == targetDirection) return false;
            stepElapsed = 0;
            int clockwise = (targetDirection - currentDirection + 16) & 15;
            currentDirection = (currentDirection + (clockwise <= 8 ? 1 : 15)) & 15;
            return true;
        }

        private static int CircularDistance(int a, int b)
        {
            int d = Math.Abs(a - b);
            return Math.Min(d, 16 - d);
        }
    }
}
