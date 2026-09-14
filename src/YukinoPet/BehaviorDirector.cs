using System;
using System.Collections.Generic;
using System.Windows;

namespace YukinoPet
{
    internal sealed class BehaviorDirector
    {
        private readonly Random random;
        private readonly CharacterState state;
        private ActionSequence sequence;
        private ActionStep activeStep;
        private double activeElapsed;
        private DateTime nextDecision;
        private DateTime nextRandomQuote;
        private DateTime nextNightReminder;
        private DateTime nextIdleVoice;
        private Point lastCursor;
        private bool hasLastCursor;
        private double hoverElapsed;
        private double peekRemaining;
        private double mouseQuoteCooldown;

        public BehaviorDirector(Random random, CharacterState state)
        {
            this.random = random;
            this.state = state;
            DateTime now = DateTime.Now;
            nextDecision = now.AddSeconds(8);
            nextRandomQuote = now.AddSeconds(random.Next(45, 91));
            nextNightReminder = now.AddMinutes(20);
            nextIdleVoice = now.AddSeconds(random.Next(20, 41));
        }

        public void Update(PetWindow host, double elapsedSeconds, DateTime now)
        {
            state.Update(elapsedSeconds, now);
            mouseQuoteCooldown = Math.Max(0, mouseQuoteCooldown - elapsedSeconds);
            host.UpdateWalking(elapsedSeconds);
            UpdateMouseAwareness(host, elapsedSeconds);
            UpdateSequence(host, elapsedSeconds);

            if (host.Settings.AnimationPaused || host.Settings.InteractionPaused || host.IsUserBusy || host.IsFocusActive || host.IsInteractionLocked) return;

            if (host.Settings.NightReminders && !host.Settings.QuietMode && IsDeepNight(now) && now >= nextNightReminder)
            {
                if (host.RequestQuote(QuoteTrigger.Night, true)) nextNightReminder = now.AddMinutes(65);
                else nextNightReminder = now.AddMinutes(10);
            }

            if (host.Settings.RandomQuotes && !host.Settings.QuietMode && now >= nextRandomQuote && sequence == null && !host.IsWalking)
            {
                QuoteTrigger trigger = state.Affection >= 45 && random.NextDouble() < 0.25 ? QuoteTrigger.Affection : QuoteTrigger.Idle;
                host.RequestQuote(trigger, false);
                nextRandomQuote = now.AddSeconds(random.Next(50, 111));
            }

            if (host.Settings.SoundEnabled && !host.Settings.QuietMode && now >= nextIdleVoice && sequence == null && !host.IsWalking && !host.IsSitting && !host.IsBubbleVisible)
            {
                host.TryPlayVoice(VoiceTrigger.Idle);
                nextIdleVoice = now.AddSeconds(random.Next(45, 91));
            }

            if (now >= nextDecision && sequence == null && !host.IsWalking && !host.IsSitting && !host.IsBubbleVisible && !host.HasBlockingAnimation)
            {
                ChooseAutonomousAction(host, now);
                nextDecision = now.AddSeconds(random.Next(8, 19));
            }
        }

        public void Interrupt(PetWindow host)
        {
            if (sequence != null && sequence.CanBeInterrupted) sequence = null;
            activeStep = null;
            activeElapsed = 0;
            host.StopMovement();
            nextDecision = DateTime.Now.AddSeconds(random.Next(3, 7));
        }

        private void UpdateMouseAwareness(PetWindow host, double elapsedSeconds)
        {
            Point cursor = host.GetCursorPositionDip();
            Point center = new Point(host.Left + host.Width / 2, host.Top + host.Height * 0.38);
            double dx = cursor.X - center.X;
            double dy = cursor.Y - center.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double speed = 0;
            if (hasLastCursor && elapsedSeconds > 0.0001)
            {
                double mx = cursor.X - lastCursor.X;
                double my = cursor.Y - lastCursor.Y;
                speed = Math.Sqrt(mx * mx + my * my) / elapsedSeconds;
            }
            lastCursor = cursor;
            hasLastCursor = true;

            if (host.Settings.InteractionPaused || host.Settings.QuietMode || host.IsUserBusy)
            {
                hoverElapsed = 0;
                host.ReleaseLook();
                return;
            }

            double awarenessRadius = 210 * host.Settings.Scale;
            if (distance <= awarenessRadius)
            {
                host.AimAt(cursor);
                hoverElapsed += elapsedSeconds;
                if (speed > 780 && distance < awarenessRadius * 0.85) state.CurrentMood = Mood.Alert;
                if (hoverElapsed > 3.2 && mouseQuoteCooldown <= 0 && host.Settings.InteractionQuotes && random.NextDouble() < 0.015)
                {
                    if (host.RequestQuote(QuoteTrigger.Mouse, false)) mouseQuoteCooldown = 45;
                }
                if (peekRemaining > 0 && distance < awarenessRadius * 0.72)
                {
                    peekRemaining = 0;
                    host.ReleaseLook();
                    if (random.NextDouble() < 0.25) host.RequestQuote(QuoteTrigger.Observation, false);
                }
            }
            else
            {
                hoverElapsed = 0;
                if (peekRemaining <= 0) host.ReleaseLook();
            }

            if (peekRemaining > 0)
            {
                peekRemaining -= elapsedSeconds;
                host.AimAt(cursor);
                if (peekRemaining <= 0) host.ReleaseLook();
            }
        }

        private void ChooseAutonomousAction(PetWindow host, DateTime now)
        {
            double roll = random.NextDouble();
            bool sleepy = state.CurrentMood == Mood.Sleepy;
            bool annoyed = state.CurrentMood == Mood.Annoyed;

            if (!annoyed && !sleepy && roll < 0.24)
            {
                Rect area = host.GetCurrentWorkArea();
                double marginX = Math.Max(16, host.Width * 0.18);
                double marginY = Math.Max(10, host.Height * 0.08);
                double targetX = area.Left + marginX + random.NextDouble() * Math.Max(1, area.Width - host.Width - marginX * 2);
                double targetY;
                if (random.NextDouble() < 0.22)
                    targetY = area.Bottom - host.Height;
                else
                    targetY = area.Top + marginY + random.NextDouble() * Math.Max(1, area.Height - host.Height - marginY * 2);
                double rest = random.Next(4, 10);
                List<ActionStep> steps = new List<ActionStep>
                {
                    ActionStep.WalkTo(targetX, targetY),
                    ActionStep.Play("idle"),
                    ActionStep.Hold(rest)
                };
                if (random.NextDouble() < 0.18) steps.Add(ActionStep.Say(QuoteTrigger.Observation));
                sequence = new ActionSequence("quiet-walk-and-rest", steps, true);
                state.CurrentAction = "walking";
                return;
            }

            if (!annoyed && roll < 0.34 && (DateTime.UtcNow - state.LastInteractionUtc).TotalSeconds > 40)
            {
                peekRemaining = 1.1 + random.NextDouble() * 1.3;
                state.CurrentAction = "peek";
                return;
            }

            if (roll < 0.40 && !sleepy)
            {
                host.BeginHeadTilt();
                state.CurrentAction = "head-tilt";
                return;
            }

            host.PlayAnimation("idle", false, null);
            state.CurrentAction = sleepy ? "resting" : "idle";
        }

        private void UpdateSequence(PetWindow host, double elapsedSeconds)
        {
            if (sequence == null) return;
            if (activeStep == null)
            {
                if (sequence.Steps.Count == 0)
                {
                    sequence = null;
                    state.CurrentAction = "idle";
                    return;
                }
                activeStep = sequence.Steps.Dequeue();
                activeElapsed = 0;
                if (activeStep.Kind == ActionStepKind.WalkTo)
                {
                    double speed = 20 + random.NextDouble() * 25;
                    host.BeginWalking(activeStep.Number, activeStep.Number2, speed);
                }
                else if (activeStep.Kind == ActionStepKind.PlayAnimation)
                {
                    host.PlayAnimation(activeStep.Text, false, null);
                }
                else if (activeStep.Kind == ActionStepKind.Quote)
                {
                    QuoteTrigger trigger;
                    if (Enum.TryParse(activeStep.Text, true, out trigger)) host.RequestQuote(trigger, false);
                }
            }

            activeElapsed += elapsedSeconds;
            bool complete = false;
            if (activeStep.Kind == ActionStepKind.WalkTo) complete = !host.IsWalking && !host.IsSitting;
            else if (activeStep.Kind == ActionStepKind.Hold) complete = activeElapsed >= activeStep.Number;
            else complete = activeElapsed >= 0.08;
            if (complete)
            {
                activeStep = null;
                activeElapsed = 0;
            }
        }

        private static bool IsDeepNight(DateTime now)
        {
            return now.Hour >= 23 || now.Hour < 6;
        }
    }
}
