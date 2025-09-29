using System; 

namespace StateAsm
{
	public abstract class GameObjectState<T> : State<T>
	{
		public GameObjectState(IStateMachine<T> stateMachine, T id) : base(stateMachine, id)
		{
		}

        public virtual void LateUpdate() {}
        public virtual void OnGUI() {}
		public virtual void OnPostRender() {}
    }
}

