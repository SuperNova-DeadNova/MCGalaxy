﻿using System;

namespace Priority_Queue
{
    /// <summary>
    /// A helper-interface only needed to make writing unit tests a bit easier
    /// </summary>
    public interface IFixedSizePriorityQueue<TItem, in TPriority> : IPriorityQueue<TItem, TPriority>
        where TPriority : IComparable<TPriority>
    {
        /// <summary>
        /// Resize the queue so it can accept more nodes.  All currently enqueued nodes are remain.
        /// Attempting to decrease the queue size to a size too small to hold the existing nodes results in undefined behavior
        /// </summary>
        public void Resize(int maxNodes);

        /// <summary>
        /// Returns the maximum number of items that can be enqueued at once in this queue.  Once you hit this number (ie. once Count == MaxSize),
        /// attempting to enqueue another item will cause undefined behavior.
        /// </summary>
        public int MaxSize { get; }

        /// <summary>
        /// By default, nodes that have been previously added to one queue cannot be added to another queue.
        /// If you need to do this, please call originalQueue.ResetNode(node) before attempting to add it in the new queue
        /// </summary>
        public void ResetNode(TItem node);
    }
}
