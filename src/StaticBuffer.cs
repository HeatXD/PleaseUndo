namespace PleaseUndo
{
    public class StaticBuffer<T>
    {
        protected int _size = 0;
        protected int _capacity; /* was template N */
        protected T[] _elements; /* was a N fixed size array */

        public StaticBuffer(int capacity)
        {
            _capacity = capacity;
            _elements = new T[capacity];
        }

        public ref T this[int i]
        {
            get
            {
                Logger.Assert(i >= 0 && i < _size);
                return ref _elements[i];
            }
        }

        public void PushBack(T t)
        {
            Logger.Assert(_size != (_capacity - 1));
            _elements[_size++] = t;
        }

        public int Size()
        {
            return _size;
        }
    }
}
