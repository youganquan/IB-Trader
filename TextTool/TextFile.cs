using System;
using System.Collections;
using System.IO;

namespace TextTool
{
	/// <summary>
	/// 提供针对文本文件的处理方法
	/// </summary>
	public class TextFile
	{
		/// <summary>
		/// 用于存储Text文件中的StringAnalyse对象
		/// </summary>
		private ArrayList textList;

		/// <summary>
		/// 分隔符
		/// </summary>
		private string delimiter;
        
		private float maxFieldNum;
		/// <summary>
		/// 行上的最大字段数
		/// </summary>
		public float MaxFieldNum
		{
			get { return maxFieldNum; }
		}


		/// <summary>
		/// 实例化Text文件处理对象
		/// </summary>
		public TextFile(string delimiter)
		{
			textList = new ArrayList();
			this.delimiter = delimiter;
            
		}

		/// <summary>
		/// 打开文件
		/// </summary>
		/// <param name="fileName"></param>
		public void OpenFile(string fileName)
		{
			textList.Clear();
			maxFieldNum = 0;

			using (StreamReader sr = new StreamReader(fileName,System.Text.Encoding.GetEncoding("gb2312")))
			{
				//1:sr.ReadToEnd();后用split('\n')在给分开
				//2:sr.Read (Char[], Int32, Int32) 
				
				string line = "";

				if (delimiter != "")
					while ((line = sr.ReadLine()) != null)
					{
						StringAnalyse stringAnalyse = new StringAnalyse();
						stringAnalyse.Analyse(line, delimiter);
						textList.Add(stringAnalyse);
						if (maxFieldNum < stringAnalyse.Length)
						{
							maxFieldNum = stringAnalyse.Length;
						}
					}
				else
				{
					while ((line = sr.ReadLine()) != null)
					{
						int[] start = new int[]{0};
						int[] end = new int[]{line.Length - 1};
						StringAnalyse stringAnalyse = new StringAnalyse();
						stringAnalyse.Analyse(line, start, end);
						textList.Add(stringAnalyse);
						if (maxFieldNum < stringAnalyse.Length)
						{
							maxFieldNum = stringAnalyse.Length;
						}
					}
				}
			}
		}

		/// <summary>
		/// 获取位于某行的StringAnalyse对象
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public StringAnalyse GetElementAt(int index)
		{
			return (StringAnalyse)textList[index];
		}

		/// <summary>
		/// 获取所有的StringAnalyse对象
		/// </summary>
		/// <returns></returns>
		public ArrayList GetAllTextList()
		{
			return textList;
		}

		/// <summary>
		/// 清空SPS文件
		/// </summary>
		public void Clear()
		{
			textList.Clear();
			maxFieldNum = 0;
		}

		/// <summary>
		/// 获取LVL文件中LVL数据的总长度
		/// </summary>
		public int Count
		{
			get
			{
				return textList.Count;
			}
		}
	}
}
