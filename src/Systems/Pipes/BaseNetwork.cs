using PowerOfMind.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PowerOfMind.Systems.Pipes
{
	public abstract class BaseNetwork<TNodeType, TNodeData, TPipeType, TPipeData> where TNodeType : INetworkMember where TPipeType : INetworkMember
	{
		protected readonly ChainList<NetworkNode> nodes = new ChainList<NetworkNode>();
		protected readonly ChainList<NetworkConnection> connections = new ChainList<NetworkConnection>();
		protected readonly ChainList<NetworkPipe> pipes = new ChainList<NetworkPipe>();
		protected int firstNodeId = -1, lastNodeId = -1;
		protected int firstPipeId = -1, lastPipeId = -1;

		private readonly HashSet<int> nodeRemoveQueue = new HashSet<int>();
		private readonly HashSet<int> pipeRemoveQueue = new();
		private readonly Dictionary<PipeKey, int> pipeAddQueue = new();

		public void Update(float dt)
		{
			EndTask();
			if(pipeRemoveQueue.Count > 0)
			{
				ProcessPipesRemove();
			}
			if(pipeAddQueue.Count > 0)
			{
				ProcessPipesAdd();
			}
			if(nodeRemoveQueue.Count > 0)
			{
				ProcessNodesRemove();
			}
			PushData();
			PullData(dt);
			StartTask(dt);
		}

		public int AddNode(TNodeType node)
		{
			NetworkNode info;
			info.ConnectionChain = -1;
			info.Instance = node;
			info.Data = default;

			int id = nodes.Add(ref firstNodeId, info);
			lastNodeId = id;

			OnNodeAdd(id);
			return id;
		}

		public void RemoveNode(int id)
		{
			nodeRemoveQueue.Add(id);
		}

		public bool AddPipe(int fromNode, int fromIndex, int toNode, int toIndex, TPipeType pipe)
		{
			if(fromNode == toNode && fromIndex == toIndex) return false;
			var key = new PipeKey(fromNode, fromIndex, toNode, toIndex);
			if(pipeAddQueue.ContainsKey(key)) return false;

			int connChain = nodes[fromNode].ConnectionChain;
			if(connChain >= 0)
			{
				foreach(var connId in connections.GetNodeEnumerable(connChain))
				{
					ref readonly var conn = ref connections[connId];
					if(conn.TargetNode == toNode && conn.TargetIndex == toIndex)
					{
						if(connections[conn.OtherConnId].TargetIndex == fromIndex)
						{
							if(pipeRemoveQueue.Contains(Math.Min(connId, conn.OtherConnId)))//changing pipe
							{
								break;
							}
							return false;
						}
					}
				}
			}

			NetworkPipe info;
			info.Instance = pipe;
			info.Data = default;
			lastPipeId = pipes.Add(ref firstPipeId, info);
			pipeAddQueue.Add(key, lastPipeId);
			return true;
		}

		public bool RemovePipe(int fromNode, int fromIndex, int toNode, int toIndex, out TPipeType pipe)
		{
			if(fromNode == toNode && fromIndex == toIndex)
			{
				pipe = default;
				return false;
			}

			if(pipeAddQueue.Remove(new PipeKey(fromNode, fromIndex, toNode, toIndex), out int id))
			{
				pipe = pipes[id].Instance;
				RemovePipe(id);
				return true;
			}

			int connChain = nodes[fromNode].ConnectionChain;
			if(connChain >= 0)
			{
				foreach(var connId in connections.GetNodeEnumerable(connChain))
				{
					ref readonly var conn = ref connections[connId];
					if(conn.TargetNode == toNode && conn.TargetIndex == toIndex)
					{
						if(connections[conn.OtherConnId].TargetIndex == fromIndex)
						{
							if(pipeRemoveQueue.Add(Math.Min(connId, conn.OtherConnId)))
							{
								pipe = pipes[conn.PipeId].Instance;
								return true;
							}
							break;
						}
					}
				}
			}

			pipe = default;
			return false;
		}

		/// <summary>
		/// Starts a processing task
		/// </summary>
		protected abstract void StartTask(float dt);
		/// <summary>
		/// Ensures the task is completed, or waiting for its completion
		/// </summary>
		protected abstract void EndTask();
		/// <summary>
		/// Retrieves data from members for processing
		/// </summary>
		protected abstract void PullData(float dt);//TODO: check for overflow when summing input throughputs for a node. i.e. just use uint.maxvalue if overflow occurs
		/// <summary>
		/// Provides processed data to members
		/// </summary>
		protected abstract void PushData();

		protected virtual void OnNodeAdd(int id) { }
		protected virtual void OnNodeRemove(int id) { }

		protected virtual void OnPipeAdd(int id) { }
		protected virtual void OnPipeRemove(int id) { }

		private void ProcessPipesRemove()
		{
			foreach(var connId in pipeRemoveQueue)
			{
				var conn = connections[connId];

				connections.Remove(ref nodes[connections[conn.OtherConnId].TargetNode].ConnectionChain, connId);
				connections.Remove(ref nodes[conn.TargetNode].ConnectionChain, conn.OtherConnId);

				OnPipeRemove(conn.PipeId);

				RemovePipe(conn.PipeId);
			}
			pipeRemoveQueue.Clear();
		}

		private void ProcessPipesAdd()
		{
			foreach(var pair in pipeAddQueue)
			{
				var conn = pair.Key;
				if(nodeRemoveQueue.Contains(conn.fromNode) || nodeRemoveQueue.Contains(conn.toNode))
				{
					RemovePipe(pair.Value);
					continue;
				}

				NetworkConnection info;
				info.PipeId = pair.Value;

				info.TargetNode = conn.toNode;
				info.TargetIndex = conn.toIndex;
				info.OtherConnId = -1;
				int connId = connections.Add(ref nodes[conn.fromNode].ConnectionChain, info);

				info.TargetNode = conn.fromNode;
				info.TargetIndex = conn.fromIndex;
				info.OtherConnId = connId;
				int otherId = connections.Add(ref nodes[conn.toNode].ConnectionChain, info);

				connections[connId].OtherConnId = otherId;

				OnPipeAdd(pair.Value);
			}
			pipeAddQueue.Clear();
		}

		private void ProcessNodesRemove()
		{
			foreach(var id in nodeRemoveQueue)
			{
				OnNodeRemove(id);

				ref var node = ref nodes[id];
				if(node.ConnectionChain >= 0)
				{
					foreach(var conn in connections.RemoveEnumerated(node.ConnectionChain))
					{
						connections.Remove(ref nodes[conn.TargetNode].ConnectionChain, conn.OtherConnId);

						OnPipeRemove(conn.PipeId);

						RemovePipe(conn.PipeId);
					}
				}

				if(id == lastNodeId)
				{
					if(!nodes.TryGetPrevId(firstNodeId, id, out lastNodeId))
					{
						lastNodeId = -1;
					}
				}
				nodes.Remove(ref firstNodeId, id);
			}
			nodeRemoveQueue.Clear();
		}

		private void RemovePipe(int id)
		{
			if(id == lastPipeId)
			{
				if(!pipes.TryGetPrevId(firstPipeId, id, out lastPipeId))
				{
					lastPipeId = -1;
				}
			}
			pipes.Remove(ref firstPipeId, id);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		protected struct NetworkNode
		{
			public TNodeType Instance;
			public int ConnectionChain;
			public TNodeData Data;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		protected struct NetworkConnection
		{
			public int PipeId;
			public int OtherConnId;
			public int TargetNode;
			public int TargetIndex;//internal member index
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		protected struct NetworkPipe
		{
			public TPipeType Instance;
			public TNodeData Data;
		}

		private readonly struct PipeKey : IEquatable<PipeKey>
		{
			public readonly int fromNode, fromIndex, toNode, toIndex;

			public PipeKey(int fromNode, int fromIndex, int toNode, int toIndex)
			{
				if(fromNode == toNode)
				{
					this.fromNode = fromNode;
					this.toNode = toNode;
					if(toIndex < fromIndex)
					{
						this.fromIndex = toIndex;
						this.toIndex = fromIndex;
					}
					else
					{
						this.fromIndex = toIndex;
						this.toIndex = fromIndex;
					}
				}
				else
				{
					if(toNode < fromNode)
					{
						this.fromNode = toNode;
						this.toNode = fromNode;
						this.fromIndex = toIndex;
						this.toIndex = fromIndex;
					}
					else
					{
						this.fromNode = fromNode;
						this.toNode = toNode;
						this.fromIndex = fromIndex;
						this.toIndex = toIndex;
					}
				}
			}

			public override bool Equals(object obj)
			{
				return obj is PipeKey key && Equals(key);
			}

			public bool Equals(PipeKey other)
			{
				return fromNode == other.fromNode &&
					   fromIndex == other.fromIndex &&
					   toNode == other.toNode &&
					   toIndex == other.toIndex;
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(fromNode, fromIndex, toNode, toIndex);
			}
		}
	}
}