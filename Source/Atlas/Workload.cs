using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{
    public struct Workload
    {
        private Func<int, int, bool> _workerFunction;
        public int _chunks;

        public Workload(Func<int, int, bool> workerFunction, int chunks)
        {
            _workerFunction = workerFunction;
            _chunks = chunks;
        }

        public bool Finish(/*int threadCount*/)
        {
            if (_chunks == 0)
                return true;
            //if (threadCount == 1 || _chunks == 1)
                return FinishSequential();
            //if (threadCount > 1)
            //    return FinishParallel(Math.Min(threadCount, _chunks));
            //return false;
        }

        private bool FinishSequential()
        {
            for (int i = 0; i < _chunks; ++i)
                if (!_workerFunction(i, 0))
                    return false;
            return true;
        }

        //private bool FinishParallel(int threadCount)
        //{
        //    bool result = true;
        //    int next = 0;
        //    object lockObj = new();

        //    List<Thread> threads = new(threadCount);
        //    var workerFunction = _workerFunction;
        //    int chunks = _chunks;
        //    for (int threadNo = 0; threadNo < threadCount; ++threadNo)
        //    {
        //        int localThreadNo = threadNo;
        //        Thread thread = new((w) =>
        //        {
        //            while (true)
        //            {
        //                int i;
        //                lock (lockObj)
        //                {
        //                    if (!result || next >= chunks)
        //                        return;
        //                    i = next++;
        //                }

        //                if (!workerFunction(i, localThreadNo))
        //                {
        //                    lock (lockObj)
        //                        result = false;
        //                    return;
        //                }
        //            }
        //        });
        //        threads.Add(thread);
        //        thread.Start();
        //    }

        //    foreach (var thread in threads)
        //        thread.Join();

        //    return result;
        //}
    }
}
