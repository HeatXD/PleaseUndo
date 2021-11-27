using System.Collections.Generic;

namespace PleaseUndo
{
    class StaticBuffer<T>
    {
        protected int _size = 0;
        protected int _capacity; /* was template N */
        protected List<T> _elements; /* was a N fixed size array */

        public StaticBuffer(int capacity)
        {
            _capacity = capacity;
            _elements = new List<T>(capacity);
        }

        public T this[int i]
        {
            get
            {
                Logger.Assert(i >= 0 && i < _size);
                return (_elements[i]);
            }
            // set
            // {
            //     Logger.Assert(i >= 0 && i < _size);
            //     _elements[i] = value;
            // }
        }

        public void push_back(T t)
        {
            Logger.Assert(_size != (_capacity - 1));
            _elements[_size++] = t;
        }

        public int size()
        {
            return _size;
        }
    }
}
