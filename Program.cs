using System;
using Substrate;
using Substrate.Core;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Reflection;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace mcmappy
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			if (args.Length < 1) {
				Console.WriteLine("Usage: mcmappy <world>");
				return;
			}
			
			_mainThread = Thread.CurrentThread;
			var timer = new System.Threading.Timer(Sample,null,TimeSpan.Zero,new TimeSpan(0,0,0,0,500));
			
			string dest = args[0];
			string dim = null;
			if (args.Length > 1) { dim = args[1]; }
			
			NbtWorld world = NbtWorld.Open(dest);
			if (world == null) {
				Console.WriteLine("world not loaded"); return;
			}
			IChunkManager cm = dim == null ? world.GetChunkManager() : world.GetChunkManager(dim);
			string name = Path.GetFileName(dest);
			
			int mxdim=0,mydim=0,mzdim=0;
			int mx=0,mz=0,sx=0,sz=0;
			HashSet<int> idset = new HashSet<int>();
			
			Console.WriteLine("Calculating size...");
			//var air = new List2D<int>();
			foreach (ChunkRef chunk in cm) {
				int xdim = chunk.Blocks.XDim;
				int ydim = chunk.Blocks.YDim;
				int zdim = chunk.Blocks.ZDim;
				if (xdim > mxdim) { mxdim = xdim; }
				if (ydim > mydim) { mydim = ydim; }
				if (zdim > mzdim) { mzdim = zdim; }
				if (chunk.X > mx) { mx = chunk.X; }
				if (chunk.Z > mz) { mz = chunk.Z; }
				if (chunk.X < sx) { sx = chunk.X; }
				if (chunk.Z < sz) { sz = chunk.Z; }
				// x, z, y is the most efficient order to scan blocks
				for(int x=0; x<xdim; x++) {
					for(int z=0;z<zdim; z++) {
						for(int y=0;y<ydim;y++) {
							int id = chunk.Blocks.GetID(x,y,z);
							idset.Add(id);
							//if (id != 0 && air[x,z] < y) { air[x,z] = y; } //keep track of highest non-air block
						}
					}
				}
			}
			double idcount = idset.Count;
			int sizex = mxdim * (mx - sx + 1);
			int sizez = mzdim * (mz - sz + 1);
			Console.WriteLine("Creating "+mydim+" "+sizex+"x"+sizez+" images");
			
			//Console.WriteLine(mxdim+" "+mydim+" "+mzdim+" "+mx+" "+mz+" "+sx+" "+sz);
			//return;
			for (int y = 0; y < mydim; y++) {
				Bitmap img = new Bitmap(sizex,sizez,PixelFormat.Format24bppRgb);
				LockBitmap lck = new LockBitmap(img);
				lck.LockBits();
			
				foreach (ChunkRef chunk in cm) { //TODO this is the slow part
					if (y >= chunk.Blocks.YDim) { continue; } //make sure the current y exists
					//if (air[chunk.X,chunk.Z] < y) { continue; } //skip if this chunk is just air
				
					int xdim = chunk.Blocks.XDim;
					int zdim = chunk.Blocks.ZDim;
					int ox = (chunk.X - sx)*mxdim;
					int oz = (chunk.Z - sz)*mzdim;

					int x=0,z=0;
					for (x = 0; x < xdim; x++) {
						for (z = 0; z < zdim; z++) {
							int id = chunk.Blocks.GetID(x,y,z);
							//BlockInfo info = chunk.Blocks.GetInfo(x, y, z);
							lck.SetPixel(ox + x,oz + z,GetColorFromId(id,idcount));
							//Console.WriteLine("["+x+","+y+","+z+"] "+info.ID+" "+info.Name);
						}
					}
				}
				lck.UnlockBits();
				img.Save(name+"_"+y+".png");
				Console.WriteLine("Saved "+name+"_"+y+".png");
			}

			_sameplingEnabled = false;
			timer.Dispose(); //kill timer.
			foreach(var kvp in _speed.OrderByDescending(k => k.Value))
			{
				Console.WriteLine(kvp.Key+"\t"+kvp.Value);
			}
			
			//TODO print out colors.. no use pallete instead
//			foreach(var kvp in _colors)
//			{
//				Console.WriteLine(
//			}

//			foreach(var kvp in _list) {
//				LockBitmap lck = kvp.Value;
//				int y = kvp.Key;
//				lck.UnlockBits();
//				Bitmap img = lck.Source;
//				img.Save(name+"_"+y);
//				Console.WriteLine("Saved "+name+"_"+y);
//			}
		}
		
//		private static Dictionary<int,LockBitmap> _list = new Dictionary<int,LockBitmap>();
//		private static LockBitmap GetImg(int y,int sizex, int sizez)
//		{
//			LockBitmap lck;
//			if (!_list.TryGetValue(y,out lck)) {
//				Bitmap img = new Bitmap(sizex,sizez,PixelFormat.Format24bppRgb);
//				lck = new LockBitmap(img);
//				lck.LockBits();
//				_list[y] = lck;
//			}
//			return lck;
//		}
		
		private static Dictionary<int,Color> _colors = new Dictionary<int,Color>();
		
		private static Color GetColorFromId(int id,double idcount)
		{
			if (id == 0) { return Color.Black; }
			Color c;
			if (!_colors.TryGetValue(id,out c)) {
				c = ColorFromHSV(_colors.Count*(360.0/idcount),1,1);
				_colors[id] = c;
			}
			return c;
		}
		
		//hue = 0,360 sat=0,1 val=0,1
		public static Color ColorFromHSV(double hue, double saturation, double value)
		{
			int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
			double f = hue / 60 - Math.Floor(hue / 60);
		
			value = value * 255;
			int v = Convert.ToInt32(value);
			int p = Convert.ToInt32(value * (1 - saturation));
			int q = Convert.ToInt32(value * (1 - f * saturation));
			int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));
		
			if (hi == 0)
				return Color.FromArgb(255, v, t, p);
			else if (hi == 1)
				return Color.FromArgb(255, q, v, p);
			else if (hi == 2)
				return Color.FromArgb(255, p, v, t);
			else if (hi == 3)
				return Color.FromArgb(255, p, q, v);
			else if (hi == 4)
				return Color.FromArgb(255, t, p, v);
			else
				return Color.FromArgb(255, v, p, q);
		}
		
		private static Thread _mainThread = null;
		private static Dictionary<string,int> _speed = new Dictionary<string, int>();
		private static bool _sameplingEnabled = true;
		private static void Sample(object state)
		{
			if (!_sameplingEnabled || _mainThread == null) { return; }
			_mainThread.Suspend();
			var st = new System.Diagnostics.StackTrace(_mainThread,false);
			StackFrame sf = st.GetFrame(0);
			MethodBase m = sf.GetMethod();
			string n = m.DeclaringType.FullName + "." + m.Name;
			if (!_speed.ContainsKey(n)) { _speed[n] = 0; }
			_speed[n]++;
			_mainThread.Resume();
		}
	}
}

