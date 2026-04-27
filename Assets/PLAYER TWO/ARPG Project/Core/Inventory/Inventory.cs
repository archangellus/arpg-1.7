using System;
using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public class Inventory
    {
        public Action<ItemInstance, InventoryCell> onItemAdded;
        public Action<ItemInstance, InventoryCell> onItemInserted;
        public Action onItemRemoved;
        public Action onMoneyChanged;
        public Action onInventoryCleared;

        protected ItemInstance[,] m_grid;
        protected int m_money;

        public static int CellSize = 52;

        /// <summary>
        /// Returns the amount of rows of this Inventory.
        /// </summary>
        public int rows { get; protected set; }

        /// <summary>
        /// Returns the amount of columns of this Inventory.
        /// </summary>
        /// <value></value>
        public int columns { get; protected set; }

        /// <summary>
        /// Returns the dictionary with all the Item Instances and their index.
        /// </summary>
        public Dictionary<ItemInstance, InventoryCell> items = new();

        /// <summary>
        /// Returns the X and Y size of the Inventory grid in pixels.
        /// </summary>
        public virtual Vector2 gridSize => new Vector2(columns, rows) * CellSize;

        /// <summary>
        /// The current amount of money on this Inventory.
        /// </summary>
        public int money
        {
            get { return m_money; }
            set
            {
                m_money = Mathf.Max(0, value);
                onMoneyChanged?.Invoke();
            }
        }

        public Inventory(int rows, int columns)
        {
            this.rows = rows;
            this.columns = columns;
            m_grid = new ItemInstance[this.rows, this.columns];
        }

        /// <summary>
        /// Returns true if a given area of the Inventory is empty.
        /// </summary>
        /// <param name="row">The index of the row you want to check.</param>
        /// <param name="column">The index of the column you want to check.</param>
        /// <param name="width">The amount of cells to check availability from the first column.</param>
        /// <param name="height">The amount of cells to check availability from the first row.</param>
        public virtual bool IsAreaEmpty(int row, int column, int width, int height)
        {
            for (int i = row; i < row + height; i++)
            {
                for (int j = column; j < column + width; j++)
                {
                    if (m_grid[i, j] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if the a given area is valid.
        /// </summary>
        /// <param name="row">The index of the row you want to check.</param>
        /// <param name="column">The index of the column you want to check.</param>
        /// <param name="width">The amount of cells to check the existence from the first column.</param>
        /// <param name="height">The amount of cells to check the existence from the first row.</param>
        public virtual bool IsAreaValid(int row, int column, int width, int height) =>
            row >= 0 && column >= 0 && row + height <= rows && column + width <= columns;

        /// <summary>
        /// Tries to stack an Item Instance on the Inventory.
        /// </summary>
        /// <param name="item">The Item Instance you want to stack.</param>
        public virtual bool TryStackItem(ItemInstance item)
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (TryStackAt(item, i, j))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to add or stack an Item Instance on the Inventory.
        /// </summary>
        /// <param name="item">The Item Instance you want to add or stack.</param>
        /// <returns>Returns true if it successfully added or stacked the item.</returns>
        public virtual bool TryAddOrStack(ItemInstance item)
        {
            if (TryStackItem(item))
                return true;

            return TryAddItem(item);
        }

        /// <summary>
        /// Tries to add an Item Instance on the Inventory in the first available space.
        /// </summary>
        /// <param name="item">The Item Instance you want to add.</param>
        /// <returns>Returns true if it successfully added the item.</returns>
        public virtual bool TryAddItem(ItemInstance item)
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (TryInsertItem(item, i, j))
                    {
                        onItemAdded?.Invoke(item, new(i, j));
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries stack an Item Instance in a given row and column.
        /// </summary>
        /// <param name="item">The Item Instance you want to stack.</param>
        /// <param name="row">The index of the Inventory row you want to stack on.</param>
        /// <param name="column">The index of the Inventory column you want to stack on.</param>
        /// <returns>Returns true if it successfully stacked the item.</returns>
        public virtual bool TryStackAt(ItemInstance item, int row, int column)
        {
            if (m_grid[row, column] == null)
                return false;

            return m_grid[row, column].TryStack(item);
        }

        /// <summary>
        /// Returns true if you can insert an Item Instance on a given row and column.
        /// </summary>
        /// <param name="item">The Item Instance you want to insert.</param>
        /// <param name="row">The index of the row you want to insert the Item Instance.</param>
        /// <param name="column">The index of the column you want to insert the Item Instance.</param>
        public virtual bool CanInsertItem(ItemInstance item, int row, int column)
        {
            return IsAreaValid(row, column, item.data.columns, item.data.rows)
                && IsAreaEmpty(row, column, item.data.columns, item.data.rows);
        }

        /// <summary>
        /// Returns true if you can insert an Item Instance on the Inventory.
        /// </summary>
        /// <param name="item">The Item Instance you want to insert.</param>
        public virtual bool CanInsertItem(ItemInstance item)
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (CanInsertItem(item, i, j))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to insert an Item Instance on a given row and column.
        /// </summary>
        /// <param name="item">The Item Instance you want to insert on the Inventory.</param>
        /// <param name="row">The row you want to add the Item Instance.</param>
        /// <param name="column">The column you want to add the Item Instance.</param>
        /// <returns>Returns true if the item was successfully inserted.</returns>
        public virtual bool TryInsertItem(ItemInstance item, int row, int column)
        {
            if (!CanInsertItem(item, row, column))
                return false;

            items.Add(item, new(row, column));

            for (int i = row; i < row + item.rows; i++)
            {
                for (int j = column; j < column + item.columns; j++)
                {
                    m_grid[i, j] = item;
                }
            }

            onItemInserted?.Invoke(item, new(row, column));
            return true;
        }

        /// <summary>
        /// Tries to remove an Item Instance from the Inventory
        /// </summary>
        /// <param name="item">The Item Instance you want to remove.</param>
        /// <returns>Returns true if the Item Instance was successfully removed.</returns>
        public virtual bool TryRemoveItem(ItemInstance item)
        {
            if (!items.ContainsKey(item))
                return false;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (m_grid[i, j] == item)
                    {
                        m_grid[i, j] = null;
                    }
                }
            }

            items.Remove(item);
            onItemRemoved?.Invoke();
            return true;
        }

        /// <summary>
        /// Returns an Item Instance from the Inventory based on its row and column.
        /// </summary>
        /// <param name="row">The row you want to get the Item Instance from.</param>
        /// <param name="column">The column you want to get Item Instance from.</param>
        public virtual ItemInstance GetItem(int row, int column) => m_grid[row, column];

        /// <summary>
        /// Returns true if the Inventory contains a given Item.
        /// </summary>
        /// <param name="item">The Item you want to check.</param>
        public virtual bool Contains(ItemInstance item) => items.ContainsKey(item);

        /// <summary>
        /// Finds the position of an Item Instance in the Inventory.
        /// </summary>
        /// <param name="item">The Item Instance you want to find.</param>
        /// <returns>Returns the InventoryCell where the Item Instance is located, or a new InventoryCell if it doesn't exist.</returns>
        public virtual InventoryCell FindPosition(ItemInstance item)
        {
            if (!items.ContainsKey(item))
                return new();

            return items[item];
        }

        /// <summary>
        /// Clears the Inventory, removing all items and resetting the grid.
        /// </summary>
        public virtual void Clear()
        {
            items.Clear();
            m_grid = new ItemInstance[rows, columns];
            onInventoryCleared?.Invoke();
        }

        /// <summary>
        /// Sorts the Inventory items based on their size (rows * columns).
        /// </summary>
        public virtual void Sort()
        {
            var sortedItems = new List<ItemInstance>(items.Keys);
            sortedItems.Sort(
                (a, b) =>
                {
                    var areaComparison = (b.rows * b.columns).CompareTo(a.rows * a.columns);
                    return areaComparison != 0 ? areaComparison : b.rows.CompareTo(a.rows);
                }
            );
            Clear();

            foreach (var item in sortedItems)
                TryAddOrStack(item);
        }
    }
}
