using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace mnn {
    public class Fifo<T> {
        private const int BASE_SIZE = 8192;

        private T[] buff;
        private int max_size = BASE_SIZE * 8;
        private int capacity;
        private int pos_start;
        private int pos_end;

        public Fifo(int size = BASE_SIZE)
        {
            Debug.Assert(size > 0);
            buff = new T[size];
            capacity = size;
            pos_end = 0;
            pos_start = 0;
        }

        public int Size()
        {
            return pos_end - pos_start;
        }

        public void Flush()
        {
            if (pos_start == 0) return;

            for (int i = 0; i < Size(); i++)
                buff[i] = buff[i + pos_start];

            pos_end -= pos_start;
            pos_start = 0;
        }

        public void Resize(int len)
        {
            if (len > max_size)
                throw new Exception(String.Format("Resize failed as the specified length {0} is larger then max_size {1}", len, max_size));

            Flush();

            T[] buff_new = new T[len];
            int pos_end = Math.Min(len, Size());

            for (int i = 0; i < pos_end; i++)
                buff_new[i] = buff[i];
            buff = buff_new;
            capacity = len;
        }

        public int FreeSpace()
        {
            Flush();
            return capacity - pos_end;
        }

        public void Append(T[] data)
        {
            if (data.Length > FreeSpace())
                Resize(capacity * 2);

            for (int i = 0; i < data.Length; i++)
                buff[pos_end + i] = data[i];
            pos_end += data.Length;
        }

        public void Append(T[] data, int len)
        {
            if (len > FreeSpace())
                Resize(capacity * 2);

            for (int i = 0; i < len; i++)
                buff[pos_end + i] = data[i];
            pos_end += len;
        }

        public void Skip(int len)
        {
            if (len > Size())
                pos_start = pos_end;
            else
                pos_start += len;
        }

        public T[] Peek()
        {
            T[] retval = new T[Size()];

            for (int i = 0; i < Size(); i++)
                retval[i] = buff[pos_start + i];

            return retval;
        }

        public T[] Take()
        {
            T[] retval = Peek();
            pos_end = pos_start = 0;
            return retval;
        }
    }
}
