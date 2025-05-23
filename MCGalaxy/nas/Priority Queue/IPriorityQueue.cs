﻿using System;
using System.Collections.Generic;

namespace Priority_Queue
{
    /// <summary>
    /// The IPriorityQueue interface.  This is mainly here for purists, and in case I decide to add more implementations later.
    /// For speed purposes, it is actually recommended that you *don't* access the priority queue through this interface, since the JIT can
    /// (theoretically?) optimize method calls from concrete-types slightly better.
    /// </summary>
    public interface IPriorityQueue<TItem, in TPriority> : IEnumerable<TItem>
        where TPriority : IComparable<TPriority>
    {
        /// <summary>
        /// Enqueue a node to the priority queue.  Lower values are placed in front. Ties are broken by first-in-first-out.
        /// See implementation for how duplicates are handled.
        /// </summary>
        public void Enqueue(TItem node, TPriority priority);

        /// <summary>
        /// Removes the head of the queue (node with minimum priority; ties are broken by order of insertion), and returns it.
        /// </summary>
        public TItem Dequeue();

        /// <summary>
        /// Removes every node from the queue.
        /// </summary>
        public void Clear();

        /// <summary>
        /// Returns whether the given node is in the queue.
        /// </summary>
        public bool Contains(TItem node);

        /// <summary>
        /// Removes a node from the queue.  The node does not need to be the head of the queue.  
        /// </summary>
        public void Remove(TItem node);

        /// <summary>
        /// Call this method to change the priority of a node.  
        /// </summary>
        public void UpdatePriority(TItem node, TPriority priority);

        /// <summary>
        /// Returns the head of the queue, without removing it (use Dequeue() for that).
        /// </summary>
        public TItem First { get; }

        /// <summary>
        /// Returns the number of nodes in the queue.
        /// </summary>
        public int Count { get; }
    }
}
