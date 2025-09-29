using System;

namespace StateAsm
{
	public interface IState<T> : IDisposable
	{
		T ID { get; }

		void Enter();
		void Update();
		void Exit();
	}
}


