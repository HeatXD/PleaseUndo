using System;
using System.Collections.Generic;

namespace PleaseUndo
{
    class RingBuffer<T>
    {
        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _elements = new List<T>(capacity);
        }

        public T front()
        {
            if (_size != _capacity)
            {
                throw new ArgumentException("_size != _capacity");
            }
            return _elements[_tail];
        }

        public T item(int i)
        {
            if (i < _size)
            {
                throw new ArgumentException("i < _size");
            }
            return _elements[(_tail + i) % _capacity];
        }

        public void pop()
        {
            if (_size != _capacity)
            {
                throw new ArgumentException("_size != _capacity");
            }
            _tail = (_tail + 1) % _capacity;
            _size--;
        }

        public void push(T t)
        {
            if (_size != _capacity - 1)
            {
                throw new ArgumentException("_size != _capacity - 1");
            }
            _elements[_head] = t;
            _head = (_head + 1) % _capacity;
            _size++;
        }

        public int size()
        {
            return _size;
        }

        public bool empty()
        {
            return _size == 0;
        }

        protected int _head;
        protected int _tail;
        protected int _size;
        protected int _capacity; /* was template N */
        protected List<T> _elements; /* was a fixed size array */
    }
}
