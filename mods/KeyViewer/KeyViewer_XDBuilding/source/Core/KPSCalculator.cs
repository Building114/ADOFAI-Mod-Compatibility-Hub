using KeyViewer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace KeyViewer.Core;

public class KPSCalculator {
    public bool Running { get; private set; }
    public int Kps;
    public int Max;
    public double Average;

    private int pressCount;
    private Profile profile;
    private CancellationTokenSource cts;
    private CancellationToken token;
    private Thread current;

    public static void Sync(IEnumerable<KPSCalculator> calcs) {
        foreach(var calc in calcs) {
            calc?.Stop();
            calc?.Start();
        }
    }
    public KPSCalculator(Profile profile) => this.profile = profile;
    public void Start() {
        Stop();

        cts = new CancellationTokenSource();
        token = cts.Token;
        current = GetCalculateThread();
        current.IsBackground = true;
        current.Name = "KeyViewer KPS Calculator";
        current.Start();
    }
    public void Stop() {
        var thread = current;
        try {
            cts?.Cancel();
            if(thread != null && thread != Thread.CurrentThread && thread.IsAlive) {
                if(!thread.Join(50)) {
                    thread.Abort();
                }
            }
        } catch { } finally {
            current = null;
            cts?.Dispose();
            cts = null;
            Running = false;
        }
    }
    public void Press() => Interlocked.Increment(ref pressCount);
    Thread GetCalculateThread() {
        return new Thread(() => {
            try {
                Running = true;
                LinkedList<int> timePoints = new();
                int prev = 0, total = 0;
                long n = 0;
                Stopwatch watch = Stopwatch.StartNew();
                while(!token.IsCancellationRequested) {
                    int updateRate = Math.Max(profile?.KPSUpdateRate ?? 1000, 1);
                    if(watch.ElapsedMilliseconds >= updateRate) {
                        int temp = Interlocked.Exchange(ref pressCount, 0);
                        int kps = temp;
                        foreach(int i in timePoints) {
                            kps += i;
                        }

                        Max = Math.Max(kps, Max);
                        if(kps != 0) {
                            Average = ((Average * n) + kps) / (n + 1.0);
                            n += 1L;
                            total += temp;
                        }
                        prev = kps;
                        timePoints.AddFirst(temp);
                        int sampleCount = Math.Max(1000 / updateRate, 1);
                        if(timePoints.Count > sampleCount) {
                            timePoints.RemoveLast();
                        }

                        Kps = kps;
                        watch.Restart();
                        Thread.Sleep(Math.Max(updateRate - 1, 0));
                    }
                }
            } catch { } finally { Running = false; }
        });
    }
}
