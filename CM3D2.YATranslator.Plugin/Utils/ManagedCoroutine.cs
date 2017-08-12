using System.Collections;
using UnityEngine;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public enum CoroutineState
    {
        Invalid,
        Ready,
        Running,
        Finished
    }

    public class ManagedCoroutine
    {
        public static readonly ManagedCoroutine NoRoutine = new ManagedCoroutine();

        private readonly IEnumerator coroutine;
        private readonly MonoBehaviour owner;

        public ManagedCoroutine(MonoBehaviour owner, IEnumerator coroutine)
        {
            this.owner = owner;
            this.coroutine = coroutine;
            State = CoroutineState.Ready;
        }

        private ManagedCoroutine() : this(null, null) { }

        public bool IsRunning => State == CoroutineState.Running;

        public CoroutineState State { get; private set; }

        public ManagedCoroutine Start()
        {
            if (State != CoroutineState.Ready)
                return this;
            owner.StartCoroutine(CoroutineManager());
            return this;
        }

        public void Stop()
        {
            if (State != CoroutineState.Running)
                return;
            owner?.StopCoroutine(coroutine);
            State = CoroutineState.Finished;
        }

        private IEnumerator CoroutineManager()
        {
            State = CoroutineState.Running;
            while (coroutine.MoveNext())
                yield return coroutine.Current;
            State = CoroutineState.Finished;
        }
    }
}