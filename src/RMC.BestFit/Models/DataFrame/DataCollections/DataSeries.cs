using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace RMC.BestFit
{
    public abstract class DataSeries : IList<Data>, INotifyCollectionChanged
    {

        /// <summary>
        /// Internal list.
        /// </summary>
        protected List<Data> _seriesOrdinates = new List<Data>();
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>
        /// Get and sets the element at the specific index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set</param>
        public Data this[int index]
        {
            get { return _seriesOrdinates[index]; }
            set
            {
                if (!ReferenceEquals(_seriesOrdinates[index], value))
                {
                    var oldvalue = _seriesOrdinates[index];
                    _seriesOrdinates[index] = value;
                    if (SuppressCollectionChanged == false)
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldvalue, index));
                }
            }
        }

        /// <summary>
        /// Suppress collection changed events from firing.
        /// </summary>
        public bool SuppressCollectionChanged { get; set; } = false;

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count => _seriesOrdinates.Count;

        /// <summary>
        /// Determines if the collection is read only. 
        /// </summary>
        public virtual bool IsReadOnly => false;

        /// <summary>
        /// Add element to the collection.
        /// </summary>
        /// <param name="item">Item to add.</param>
        public virtual void Add(Data item)
        {
            _seriesOrdinates.Add(item);
            if (SuppressCollectionChanged == false)
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _seriesOrdinates.Count - 1));
        }

        /// <summary>
        /// Inserts an element into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the element should be inserted.</param>
        /// <param name="item">Item to insert.</param>
        public virtual void Insert(int index, Data item)
        {
            _seriesOrdinates.Insert(index, item);
            if (SuppressCollectionChanged == false)
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        /// <summary>
        /// Removes the first occurrence of the specified object.
        /// </summary>
        /// <param name="item">The object to remove from the collection.</param>
        public virtual bool Remove(Data item)
        {
            var index = IndexOf(item);
            if (_seriesOrdinates.Remove(item) == true)
            {
                if (SuppressCollectionChanged == false)
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove element at the specified index of the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        public virtual void RemoveAt(int index)
        {
            Remove(_seriesOrdinates[index]);
        }

        /// <summary>
        /// Remove all elements from the collection.
        /// </summary>
        public void Clear()
        {
            SuppressCollectionChanged = true;
            try
            {
                for (int i = _seriesOrdinates.Count - 1; i >= 0; i--)
                    Remove(_seriesOrdinates[i]);
            }
            finally
            {
                SuppressCollectionChanged = false;
            }
            RaiseCollectionChangedReset();
        }

        /// <summary>
        /// Determines whether an element is in the collection.
        /// </summary>
        /// <param name="item">The item to locate.</param>
        public bool Contains(Data item)
        {
            return _seriesOrdinates.Contains(item);
        }

        /// <summary>
        /// Copies the entire collection to a compatible one-dimensional array starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the copied elements.</param>
        /// <param name="arrayIndex">The zero-based index in the array at which copying begins.</param>
        public void CopyTo(Data[] array, int arrayIndex)
        {
            _seriesOrdinates.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire collection.
        /// </summary>
        /// <param name="item">The object to locate in the collection.</param>
        public int IndexOf(Data item)
        {
            return _seriesOrdinates.IndexOf(item);
        }

        public IEnumerator<Data> GetEnumerator()
        {
            return _seriesOrdinates.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Raise the collection changed reset event.
        /// </summary>
        public void RaiseCollectionChangedReset()
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }


        /// <summary>
        /// Returns the list of series values as a list.
        /// </summary>
        public List<double> ValuesToList()
        {
            return _seriesOrdinates.Select(x => x.Value).ToList();
        }

        /// <summary>
        /// Returns the list of series values as an array.
        /// </summary>
        public double[] ValuesToArray()
        {
            return _seriesOrdinates.Select(x => x.Value).ToArray();
        }


        /// <summary>
        /// Returns the list of series plotting positions as a list.
        /// </summary>
        public List<double> PlottingPositionsToList()
        {
            return _seriesOrdinates.Select(x => x.PlottingPosition).ToList();
        }

        /// <summary>
        /// Returns the list of series plotting positions as an array.
        /// </summary>
        public double[] PlottingPositionsToArray()
        {
            return _seriesOrdinates.Select(x => x.PlottingPosition).ToArray();
        }


        /// <summary>
        /// Returns the list of series indices as a list.
        /// </summary>
        public List<int> IndicesToList()
        {
            return _seriesOrdinates.Select(x => x.Index).ToList();
        }

        /// <summary>
        /// Returns the list of series indices as an array.
        /// </summary>
        public int[] IndicesToArray()
        {
            return _seriesOrdinates.Select(x => x.Index).ToArray();
        }

    }
}
