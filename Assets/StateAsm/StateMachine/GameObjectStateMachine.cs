using UnityEngine;
using System.Collections.Generic;
using System;

namespace StateAsm
{
	public class GameObjectStateMachine<T> : MonoBehaviour, IStateMachine<T>
	{
		protected GameObjectState<T>	currentState;
		protected GameObjectState<T>	prevState;
		protected GameObjectState<T>	nextState;
		protected Dictionary<T, GameObjectState<T>> states;

		//-------------------------------------------------------------------
		// Acessors
		//-------------------------------------------------------------------

		public IState<T> CurrentState 
		{
			get{ return currentState; }
		}
	
        public IState<T> PreviousState
        {
            get{ return prevState; }
        }

		//-------------------------------------------
		// Monobehaviour
		//-------------------------------------------

		protected virtual void Awake() 
		{
			//Debug.Log ("-> GameObjectStateMachine::Awake");
			InitStateMachine();
		}

		protected virtual void Start() 
		{
			//Debug.Log("-> GameObjectStateMachine::Start");
			InitStates();
		}

	 	protected virtual void Update() 
		{      
			currentState?.Update(); 
		}

        protected virtual void FixedUpdate() {}

        protected virtual void LateUpdate()
        {
			currentState?.LateUpdate();   
        }

        //-------------------------------------------
        // StateMachine 
        //-------------------------------------------

        private void InitStateMachine()
		{
            currentState = null;
			states = new Dictionary<T, GameObjectState<T>>();	
		}
	
		protected virtual void InitStates() {}

	
		public void AddState( IState<T> state )
		{
			this.states.Add( state.ID, state as GameObjectState<T>);
        }
		

		public bool SetState( IState<T> state)
		{
            if (state != null)
			{
                currentState?.Exit();
				
				prevState = currentState;
				currentState = state as GameObjectState<T>;
				currentState.Enter();
                return true;
			}
			else
			{
				return false;
			}
		}
		

		public bool SetState(T stateID)
		{
            if (currentState != null)
            {
                if (stateID.Equals(currentState.ID))
                    return true;
            }

            if(!states.ContainsKey(stateID))
            {
                Debug.LogError("LL-> GameObjectStateMachine::ChangeState() - Can't find stateID: " + stateID);
                return false;
            }

            return SetState( states[stateID] );
		}

        public void ClearStates()
        {
            //Dispose of resources within each state
            foreach (KeyValuePair<T, GameObjectState<T>> state in states)
            {
                state.Value.Dispose();
            }

            currentState = null;
            prevState = null;
            nextState = null;

            states.Clear();
            states = null;
        }

        protected virtual void OnDestroy()
        {
            ClearStates();
        }
    }
}

