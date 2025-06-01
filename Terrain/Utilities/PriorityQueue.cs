using System.Collections.Generic;
using System;

public class PriorityQueue<T>
{
    private IEqualityComparer<T> comparer;
    private readonly List<(T Item, int Priority)> heap = new();

    public PriorityQueue()
        : this(EqualityComparer<T>.Default)
    {

    }

    public PriorityQueue(IEqualityComparer<T> comparer)
    {

    }

    public int Count => heap.Count;

    public void Enqueue(T item, int priority)
    {
        heap.Add((item, priority));
        HeapifyUp(heap.Count - 1);
    }

    public T Dequeue()
    {
        if (heap.Count == 0)
            return default(T);

        T result = heap[0].Item;
        heap[0] = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        HeapifyDown(0);
        return result;
    }

    public bool TryDequeue(out T item)
    {
        if (heap.Count > 0)
        {
            item = Dequeue();
            return true;
        }

        item = default;
        return false;
    }

    public T Peek()
    {
        if (heap.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        return heap[0].Item;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (heap[index].Priority >= heap[parent].Priority)
                break;

            (heap[index], heap[parent]) = (heap[parent], heap[index]);
            index = parent;
        }
    }

    private void HeapifyDown(int index)
    {
        int lastIndex = heap.Count - 1;
        while (true)
        {
            int smallest = index;
            int left = 2 * index + 1;
            int right = 2 * index + 2;

            if (left <= lastIndex && heap[left].Priority < heap[smallest].Priority)
                smallest = left;
            if (right <= lastIndex && heap[right].Priority < heap[smallest].Priority)
                smallest = right;

            if (smallest == index)
                break;

            (heap[index], heap[smallest]) = (heap[smallest], heap[index]);
            index = smallest;
        }
    }

    public bool Remove(T item)
    {
        if (this.heap.Count == 0) return false;

        int index = heap.FindIndex(entry => comparer.Equals(entry.Item, item));
        if (index == -1)
            return false;

        int lastIndex = heap.Count - 1;
        if (index != lastIndex)
        {
            heap[index] = heap[lastIndex];
            heap.RemoveAt(lastIndex);
            HeapifyDown(index);
            HeapifyUp(index);
        }
        else
        {
            heap.RemoveAt(index);
        }

        return true;
    }

    public bool RemoveWhere(Predicate<T> match)
    {
        if (this.heap.Count == 0) return false;

        int index = heap.FindIndex(entry => match(entry.Item));
        if (index == -1)
            return false;

        int lastIndex = heap.Count - 1;
        if (index != lastIndex)
        {
            heap[index] = heap[lastIndex];
            heap.RemoveAt(lastIndex);
            HeapifyDown(index);
            HeapifyUp(index);
        }
        else
        {
            heap.RemoveAt(index);
        }

        return true;
    }
}
