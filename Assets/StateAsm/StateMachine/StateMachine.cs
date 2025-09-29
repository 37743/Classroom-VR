//Finite State Machine
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace StateAsm
{
	public class StateMachine<T> : IStateMachine<T>
	{
		protected IState<T> currentState;
		protected IState<T> prevState;
		protected Dictionary<T, IState<T>> states;
					
		public IState<T> CurrentState 
		{
			get{ return currentState; }
		}

		public IState<T> PreviousState
		{
			get{ return prevState; }
		}
		
		
		//------------------------
		// Methods
		//------------------------
		
		public StateMachine ()
		{
			currentState = null;
			states = new Dictionary<T, IState<T>>();				
		}
				
		public void Update()
		{
            if (currentState != null)
            {
                currentState.Update();
            }
		}

        public void AddState( IState<T> state )
		{
			//Debug.Log("-> StateMachine::AddState() - state ID: " + state.ID);
			this.states.Add( state.ID, state );
		}
				
        public bool SetState(IState<T> state)
        {
            if (state != null)
            {
                currentState?.Exit();

                prevState = currentState;
                currentState = state;
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
                //although we are trying to change to the what is already the current state.
                //this is, after all, the state that we want, so lets return true.
                if (stateID.Equals(currentState.ID))
                    return true;
            }

            return SetState(states[stateID]);
        }


        public void ClearStates()
        {
            // Dispose of resources within each state
            foreach (var state in states.Values)
            {
                state?.Dispose();
            }

            // Clear the dictionary to remove all keys and values
            states.Clear();

            currentState = null;
            prevState = null;
        }
    }
}


