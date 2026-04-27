using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class QuestInstance
    {
        public enum State
        {
            InProgress = 1,
            ReturnToGiver = 2,
            Completed = 3,
        }

        public Quest data;
        public System.Action<State> onStateChanged;

        protected int m_progress;
        protected State m_state = State.InProgress;

        /// <summary>
        /// The current progress of this Quest Instance.
        /// </summary>
        public int progress
        {
            get { return m_progress; }
            set
            {
                if (!data.IsProgress())
                    return;

                m_progress = Mathf.Clamp(value, 0, data.targetProgress);
            }
        }

        /// <summary>
        /// Returns true if this Quest is completed.
        /// </summary>
        public bool completed => m_state == State.Completed;

        /// <summary>
        /// Returns true if this Quest is in progress.
        /// </summary>
        public bool inProgress => m_state == State.InProgress;

        /// <summary>
        /// Returns true if the state of this Quest Instance is ReturnToGiver.
        /// </summary>
        public bool returningToGiver => m_state == State.ReturnToGiver;

        public State state
        {
            get => m_state;
            protected set { m_state = value; }
        }

        public QuestInstance(Quest data)
        {
            this.data = data;
        }

        public QuestInstance(Quest data, int progress, State state)
        {
            this.data = data;
            this.progress = progress;
            this.state = state;
        }

        public QuestInstance(Quest data, int progress, int state)
        {
            this.data = data;
            this.progress = progress;
            this.state = (State)state;
        }

        /// <summary>
        /// Changes the state of this Quest Instance.
        /// </summary>
        /// <param name="target">The target state you want to change to.</param>
        protected virtual void ChangeState(State target)
        {
            if (m_state == target)
                return;

            m_state = target;
            onStateChanged?.Invoke(m_state);
        }

        /// <summary>
        /// Changes the state of this Quest Instance to the next state based on the current state and data.
        /// </summary>
        public virtual void NextState()
        {
            switch (m_state)
            {
                default:
                case State.Completed:
                    break;
                case State.InProgress:
                    if (data.returnToGiver)
                        ChangeState(State.ReturnToGiver);
                    else
                        ChangeState(State.Completed);
                    break;
                case State.ReturnToGiver:
                    ChangeState(State.Completed);
                    break;
            }
        }

        public virtual void AddProgressScene(string scene)
        {
            if (!inProgress || !data.IsReachScene() || !data.IsDestinationScene(scene))
                return;

            NextState();
        }

        public virtual void AddProgressKey(string key)
        {
            if (!inProgress || !data.IsProgress() || !data.IsProgressKey(key))
                return;

            progress++;

            if (progress >= data.targetProgress)
                NextState();
        }

        public virtual void AddProgressTrigger()
        {
            if (!inProgress || !data.IsTrigger())
                return;

            NextState();
        }

        /// <summary>
        /// Returns the formatted progress text.
        /// </summary>
        public virtual string GetProgressText()
        {
            if (data.IsProgress() && progress < data.targetProgress)
                return $"Progress: {progress} / {data.targetProgress}";
            else
                return StateToString();
        }

        /// <summary>
        /// Rewards a given Entity with all the Quest's rewards.
        /// </summary>
        /// <param name="entity">The Entity you want to reward.</param>
        public virtual void Reward(Entity entity)
        {
            if (!entity)
                return;

            if (entity.stats)
                entity.stats.AddExperience(data.experience);

            if (entity.inventory)
            {
                entity.inventory.instance.money += data.coins;

                foreach (var item in data.items)
                {
                    entity.inventory.instance.TryAddItem(item.CreateItemInstance());
                }
            }
        }

        /// <summary>
        /// Returns true if the Quest's completing mode is 'Progress' and a given key matches the Quest's progress key.
        /// This is used to check if a key is valid for adding progress to this Quest Instance.
        /// </summary>
        /// <param name="key">The progress key you want to check.</param>
        /// <returns>Returns true if the key is valid for progress.</returns>
        public virtual bool IsProgressKey(string key) =>
            data.IsProgress() && data.IsProgressKey(key);

        protected virtual string StateToString()
        {
            return m_state switch
            {
                State.ReturnToGiver => "Return to Giver",
                State.Completed => "Completed",
                _ => "In Progress",
            };
        }

        /// <summary>
        /// Returns the current objective of this Quest Instance.
        /// If the Quest is in the ReturnToGiver state, it returns "Return to Giver".
        /// </summary>
        public virtual string CurrentObjective()
        {
            return m_state switch
            {
                State.ReturnToGiver => "Return to Giver",
                _ => data.objective,
            };
        }
    }
}
