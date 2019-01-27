﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lockstep.Core.Data;
using Lockstep.Core.Interfaces;

namespace Lockstep.Client.Implementations
{                
    public class CommandBuffer : ICommandBuffer
    {                                   
        /// <summary>
        /// Mapping: FrameNumber -> Commands per player(Id)
        /// </summary>
        private readonly Dictionary<long, Dictionary<byte, List<ICommand>>> _buffer = new Dictionary<long, Dictionary<byte, List<ICommand>>>(5000);    

        public void Lock()
        {
            _mutex.WaitOne();
        }

        public void Release()
        {
            _mutex.ReleaseMutex();
        }

        public long NextFrameIndex { get; private set; }

        private readonly Mutex _mutex = new Mutex();

        public virtual void Insert(byte commanderId, long frameNumber, ICommand[] commands)
        {
            _mutex.WaitOne();

            if (!_buffer.ContainsKey(frameNumber))
            {
                _buffer.Add(frameNumber, new Dictionary<byte, List<ICommand>>(10)); //Initial size for 10 players
            }

            if (!_buffer[frameNumber].ContainsKey(commanderId))
            {
                _buffer[frameNumber].Add(commanderId, new List<ICommand>(5)); //Initial size of 5 commands per frame
            }

            //TODO: order by timestamp in case of multiple commands in the same frame => if commands intersect, the first one should win, requires !serverside! timestamp
            //ordering is enough, validation should take place in the simulation(core)
            _buffer[frameNumber][commanderId].AddRange(commands);

            if (frameNumber < NextFrameIndex)
            {
                NextFrameIndex = frameNumber;
            }

            _mutex.ReleaseMutex();
        }

        public ICommand[] GetNext()
        {
            _mutex.WaitOne();
            //If no commands were inserted then return an empty list
            if (!_buffer.ContainsKey(NextFrameIndex))
            {
                    _buffer.Add(NextFrameIndex, new Dictionary<byte, List<ICommand>>());
            }

            var result = _buffer[NextFrameIndex++].SelectMany(pair => pair.Value).ToArray();

            _mutex.ReleaseMutex();

            return result;
        }
    }
}
