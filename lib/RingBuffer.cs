namespace PleaseUndo
{
    public class RingBuffer<T>
    {
        protected int _head = 0;
        protected int _tail = 0;
        protected int _size = 0;
        protected int _capacity; /* was template N */
        protected readonly T[] _elements; /* was a N fixed size array */

        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _elements = new T[capacity];
        }

        public void pop()
        {
            Logger.Assert(_size != _capacity);
            _tail = (_tail + 1) % _capacity;
            _size--;
        }

        public void push(T t)
        {
            Logger.Assert(_size != _capacity - 1);
            _elements[_head] = t;
            _head = (_head + 1) % _capacity;
            _size++;
        }

        public T item(int i)
        {
            Logger.Assert(i < _size);
            return _elements[(_tail + i) % _capacity];
        }

        public T front()
        {
            Logger.Assert(_size != _capacity);
            return _elements[_tail];
        }

        public int size()
        {
            return _size;
        }

        public bool empty()
        {
            return _size == 0;
        }
    }
}
