/* ============================================================================
The MIT License (MIT)

Copyright (c) 2013 Rasberry

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
============================================================================ */
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

namespace MCMapSlices
{
	class MainClass
	{
		private const bool Profile = false;
		public static void Main(string[] args)
		{
			if (args.Length < 1) {
				Console.WriteLine("Usage: MCMapSlices (world) -p (pallete) [-m MiB] [-d dimension] [-c imgcount]");
				return;
			}
			
			//poor mans cpu profiler
			System.Threading.Timer timer = null;
			if (Profile) {
				_mainThread = Thread.CurrentThread;
				timer = new System.Threading.Timer(Sample,null,TimeSpan.Zero,new TimeSpan(0,0,0,0,500));
			}
			
			string dest=null,dim=null,pal=null,scount=null,smegs=null;
			for(int a=0; a<args.Length; a++) {
				string c = args[a];
				if (c == "-d") { dim = args[++a]; }
				else if (c == "-p") { pal = args[++a]; }
				else if (c == "-m") { smegs = args[++a]; }
				else if (c == "-c") { scount = args[++a]; }
				else { dest = args[a]; }
			}
			
			if (String.IsNullOrEmpty(pal)) {
				Console.WriteLine("no palette file specified. use -p (palette file)");
				return;
			}
			LoadPalette(pal);
			NbtWorld world = NbtWorld.Open(dest);
			if (world == null) {
				Console.WriteLine("world not loaded");
				return;
			}
			
			IChunkManager cm = dim == null ? world.GetChunkManager() : world.GetChunkManager(dim);
			string name = Path.GetFileName(dest) + (dim ?? "");
			
			int mxdim=0,mydim=0,mzdim=0;
			int mx=0,mz=0,sx=0,sz=0;
			
			Console.WriteLine("Calculating size...");
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
			}
			int sizex = mxdim * (mx - sx + 1);
			int sizez = mzdim * (mz - sz + 1);
			Console.WriteLine("Creating "+mydim+" "+sizex+"x"+sizez+" images");

			double maxmegs; int imgcount = 1;
			if (smegs != null && double.TryParse(smegs,out maxmegs) && maxmegs > 0)
			{
				imgcount = Math.Max(CalcCount(sizex,sizez,maxmegs*1024*1024),1);
			}
			else if (scount != null && (!int.TryParse(scount,out imgcount) || imgcount < 1))
			{
				imgcount = 1;
			}
			Console.WriteLine("Batching images "+imgcount+" at a time");

			LockBitmap[] batch = new LockBitmap[imgcount];
			
			int by = 0,imgc = imgcount;
			while(by < mydim) {
				if (by + imgcount > mydim) { imgc = mydim - by; }
				for(int b=0; b<imgc; b++) {
					Bitmap img = new Bitmap(sizex,sizez,PixelFormat.Format24bppRgb);
					LockBitmap lck = new LockBitmap(img);
					lck.LockBits();
					batch[b] = lck;
				}
			
				foreach (ChunkRef chunk in cm) { //TODO this is the slow part
					int ydim = chunk.Blocks.YDim;
					int xdim = chunk.Blocks.XDim;
					int zdim = chunk.Blocks.ZDim;
					int ox = (chunk.X - sx)*mxdim;
					int oz = (chunk.Z - sz)*mzdim;

					int x=0,z=0,y=0,b=0;
					for(b = 0; b < imgc; b++) {
						y = by + b; //current batch start + batch number
						if (y >= ydim) { continue; }
						for (x = 0; x < xdim; x++) {
							for (z = 0; z < zdim; z++) {
								int id = chunk.Blocks.GetID(x,y,z);
								int meta = chunk.Blocks.GetData(x,y,z);
								LockBitmap lck = batch[b];
								lck.SetPixel(ox + x,oz + z,GetColorFromId(id,meta));
							}
						}
					}
				}

				for(int b=0; b<imgc; b++) {
					LockBitmap lck = batch[b];
					lck.UnlockBits();
					int y = by + b;
					string outfile = name+"_"+y.ToString("000")+".png";
					lck.Source.Save(outfile);
					Console.WriteLine("Saved "+outfile);
				}
				by += imgc;
			}

			if (Profile) {
				_sameplingEnabled = false;
				timer.Dispose(); //kill timer.
				foreach(var kvp in _speed.OrderByDescending(k => k.Value)) {
					Console.WriteLine(kvp.Key+"\t"+kvp.Value);
				}
			}
			Console.WriteLine("Max memory "+Process.GetCurrentProcess().PeakWorkingSet64/1024/1024+" Megs");
		}

		//curve fitting courtesy of http://zunzun.com/
		private static double CalcMemory(int w,int h,int count)
		{
			double a = 10.3626219049676;
			double o = 193701151.975195;
			return o + a*w*h*count;
		}
		private static int CalcCount(int w, int h, double bytes)
		{
			double a = 10.3626219049676;
			double o = 193701151.975195;
			return (int)((bytes - o) / (a*w*h)); //implicit floor
		}

		private static Dictionary<int,Color> _colors = new Dictionary<int,Color>();
		
		private static Color GetColorFromId(int id, int meta)
		{
			int key = meta !=0 ? -1*(meta + (id << 4)) : id;
			Color c;
			if (!_colors.TryGetValue(key, out c)) { //try id+meta
				if (!_colors.TryGetValue(id, out c)) { //try just id
					c = Color.Black;
				}
			}
			return c;
		}
		
		//. 140 * 128 192 100 64 146
		//. id meta ? r g b ?
		private static void LoadPalette(string file)
		{
			StreamReader sr = File.OpenText(file);
			while(!sr.EndOfStream) {
				string line = sr.ReadLine();
				if (line.StartsWith(".")) {
					string[] ops = line.Split(new char[] {' '},StringSplitOptions.RemoveEmptyEntries);
					if (ops.Length < 8) { continue; }
					int id,meta,r,g,b;
					if (
						int.TryParse(ops[1],out id)
						&& int.TryParse(ops[4], out r)
						&& int.TryParse(ops[5],out g)
						&& int.TryParse(ops[6],out b)
					) {
						if (!int.TryParse(ops[2], out meta)) { meta = 0; }
						int key = meta !=0 ? -1*(meta + (id << 4)) : id;
						Color c = Color.FromArgb(r,g,b);
						_colors[key] = c;
					}
				}
			}
		}
		
		private static Color GetColorFromId_old(int id,double idcount)
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
		private static bool _sameplingEnabled = Profile;
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

