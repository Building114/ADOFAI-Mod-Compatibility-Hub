using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Overlayer.Unity;

public class StaticCoroutine : MonoBehaviour {
                                                 
                                     
    static int mainThreadId = -1;
    static StaticCoroutine Runner {
        get {
            if(!runner) {
                runner = new GameObject().AddComponent<StaticCoroutine>();
                DontDestroyOnLoad(runner.gameObject);
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
                return runner;
            }
            return runner;
        }
    }
    static StaticCoroutine runner;
    static readonly object routinesLock = new();
    static readonly Queue<IEnumerator> routines = new();
    public static bool IsMainThread => mainThreadId == -1 || Thread.CurrentThread.ManagedThreadId == mainThreadId;
    public static Coroutine Run(IEnumerator coroutine) {
                                                           
                                                
                                                        
        if(!IsMainThread) {
            Queue(coroutine);
            return null;
        }
        if(coroutine == null) {
            _ = Runner;
            return null;
        }
        return Runner.StartCoroutine(coroutine);
    }
    public static void Queue(IEnumerator coroutine) {
        if(coroutine == null) {
            return;
        }
                                           
        lock(routinesLock) {
            routines.Enqueue(coroutine);
        }
    }
    public static IEnumerator SyncRunner(Action routine, object firstYield = null) {
        yield return firstYield;
        routine?.Invoke();
        yield break;
    }
    void Update() {
        while(true) {
            IEnumerator routine;
            lock(routinesLock) {
                if(routines.Count == 0) {
                    break;
                }
                routine = routines.Dequeue();
            }
            StartCoroutine(routine);
        }
    }
}
