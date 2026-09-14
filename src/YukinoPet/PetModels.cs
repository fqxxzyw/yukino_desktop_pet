using System;
using System.Collections.Generic;

namespace YukinoPet
{
    internal enum PetBehaviorState
    {
        Idle,
        Blink,
        HeadTilt,
        Walk,
        Greeting,
        Interaction,
        Sitting,
        Talking,
        Dragging
    }

    internal enum Mood
    {
        Idle,
        Calm,
        Bored,
        Curious,
        Focused,
        Sleepy,
        Alert,
        Annoyed,
        Relaxed,
        Talkative
    }

    internal enum QuoteTrigger
    {
        Idle,
        Mouse,
        Click,
        HeadPat,
        Annoyed,
        Night,
        Work,
        Break,
        Greeting,
        Farewell,
        Affection,
        Observation
    }

    internal sealed class CharacterState
    {
        public Mood CurrentMood = Mood.Calm;
        public double Affection = 12;
        public double Trust = 10;
        public double Familiarity = 8;
        public double Annoyance;
        public DateTime LastInteractionUtc = DateTime.UtcNow;
        public DateTime LastQuoteUtc = DateTime.MinValue;
        public string CurrentAction = "idle";

        public void RegisterInteraction(double affection, double trust, double annoyance)
        {
            Affection = Clamp(Affection + affection, 0, 100);
            Trust = Clamp(Trust + trust, 0, 100);
            Familiarity = Clamp(Familiarity + 0.2, 0, 100);
            Annoyance = Clamp(Annoyance + annoyance, 0, 100);
            LastInteractionUtc = DateTime.UtcNow;
            RefreshMood(DateTime.Now);
        }

        public void Update(double elapsedSeconds, DateTime localNow)
        {
            Annoyance = Math.Max(0, Annoyance - elapsedSeconds * 0.035);
            RefreshMood(localNow);
        }

        private void RefreshMood(DateTime localNow)
        {
            if (Annoyance >= 32) CurrentMood = Mood.Annoyed;
            else if (localNow.Hour >= 23 || localNow.Hour < 6) CurrentMood = Mood.Sleepy;
            else if ((DateTime.UtcNow - LastInteractionUtc).TotalMinutes > 8) CurrentMood = Mood.Bored;
            else if (Trust >= 55 && Affection >= 50) CurrentMood = Mood.Relaxed;
            else CurrentMood = Mood.Calm;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    internal sealed class QuoteEntry
    {
        public string Text { get; set; }
        public string Category { get; set; }
        public string TriggerType { get; set; }
        public int Weight { get; set; }
        public int CooldownSeconds { get; set; }
        public double MinAffection { get; set; }
        public double MaxAffection { get; set; }
        public string RequiredState { get; set; }
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public bool AllowRepeat { get; set; }
        public string RequiredAction { get; set; }
        public int Priority { get; set; }
    }

    internal enum ActionStepKind
    {
        WalkTo,
        PlayAnimation,
        Hold,
        Quote
    }

    internal sealed class ActionStep
    {
        public ActionStepKind Kind;
        public double Number;
        public double Number2;
        public string Text;

        public static ActionStep WalkTo(double x, double y) { return new ActionStep { Kind = ActionStepKind.WalkTo, Number = x, Number2 = y }; }
        public static ActionStep Play(string animation) { return new ActionStep { Kind = ActionStepKind.PlayAnimation, Text = animation }; }
        public static ActionStep Hold(double seconds) { return new ActionStep { Kind = ActionStepKind.Hold, Number = seconds }; }
        public static ActionStep Say(QuoteTrigger trigger) { return new ActionStep { Kind = ActionStepKind.Quote, Text = trigger.ToString() }; }
    }

    internal sealed class ActionSequence
    {
        public readonly string Name;
        public readonly Queue<ActionStep> Steps;
        public bool CanBeInterrupted;

        public ActionSequence(string name, IEnumerable<ActionStep> steps, bool canBeInterrupted)
        {
            Name = name;
            Steps = new Queue<ActionStep>(steps);
            CanBeInterrupted = canBeInterrupted;
        }
    }
}
