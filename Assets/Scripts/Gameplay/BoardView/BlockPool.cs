using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// Bricks come from a pool (`.claude/rules/gameplay.md`). With at most 128 blocks on a
    /// board the first level costs 128 instantiations either way — what the pool buys is
    /// every level after it, and the guarantee that a brick is never created while a move is
    /// being watched.
    /// <para>
    /// A plain C# class, not a component: it is constructed by <see cref="BoardView"/> and
    /// has no reason to appear in a scene.
    /// </para>
    /// </summary>
    public sealed class BlockPool
    {
        private readonly BlockView prefab;
        private readonly Transform idleParent;
        private readonly Stack<BlockView> idle = new Stack<BlockView>();

        public BlockPool(BlockView prefab, Transform idleParent)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (idleParent == null)
            {
                throw new ArgumentNullException(nameof(idleParent));
            }

            this.prefab = prefab;
            this.idleParent = idleParent;
        }

        /// <summary>How many bricks are parked and ready.</summary>
        public int IdleCount => idle.Count;

        /// <summary>How many bricks this pool has ever created — the high-water mark.</summary>
        public int CreatedCount { get; private set; }

        public BlockView Take()
        {
            BlockView block;

            if (idle.Count > 0)
            {
                block = idle.Pop();
            }
            else
            {
                block = UnityEngine.Object.Instantiate(prefab, idleParent);
                CreatedCount++;
            }

            block.gameObject.SetActive(true);
            return block;
        }

        public void Return(BlockView block)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            block.gameObject.SetActive(false);
            block.transform.SetParent(idleParent, false);
            block.transform.localPosition = Vector3.zero;
            idle.Push(block);
        }
    }
}
