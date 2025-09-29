using System;

namespace StateAsm
{
	
	public abstract class State<T> : IState<T>
	{
        protected T id;
		protected IStateMachine<T> stateMachine;

        protected Boolean isDisposed = false;


		public T ID 
		{
			get { return id; }
		}
		
		public State(IStateMachine<T> stateMachine, T id)
		{
			this.id = id;
			this.stateMachine = stateMachine;
		}

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();

        public virtual void Dispose()
        {
            id = default(T);
            stateMachine = null;
            isDisposed = true;
        }      
	}
}


