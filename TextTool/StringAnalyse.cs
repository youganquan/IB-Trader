using System;
using System.Text;
using System.Collections;

namespace TextTool
{
	/// <summary>
	/// 按照相应规则将字符串分解成若干单词。
	/// </summary>
	public class StringAnalyse
	{
		/// <summary>
		/// 存放单词的数组
		/// </summary>
		private string[] tokens;

		/// <summary>
		/// 分解以单一字符串分隔开的若干单词
		/// </summary>
		/// <param name="dataLine">待解析的字符串</param>
		/// <param name="delimiter">分隔符</param>
		public void Analyse(string dataLine,string delimiter)
		{
			dataLine = dataLine.Trim();
			ArrayList temp = new ArrayList();
			int index;
			while ((index = dataLine.IndexOf(delimiter)) != -1)
			{
				temp.Add(dataLine.Substring(0, index));
				dataLine = dataLine.Substring(index + 1).Trim();
			}
			temp.Add(dataLine);
			tokens = new string[temp.Count];
			for (int i = 0; i < tokens.Length; i++)
			{
				tokens[i] = temp[i].ToString() ;
			}
		}

		/// <summary>
		/// 分解以字符串数组分隔开的若干单词
		/// </summary>
		/// <param name="dataLine">待解析的字符串</param>
		/// <param name="delimiters">分隔符数组</param>
		public void Analyse(string dataLine, string[] delimiters)
		{
			dataLine.Trim();
			tokens = new string[delimiters.Length + 1];

			for (int i = 0; i < delimiters.Length; i++)
			{
				int delimiterIndex = dataLine.IndexOf(delimiters[i]);
				tokens[i] = dataLine.Substring(0,delimiterIndex);
				tokens[i].Trim();
				dataLine = dataLine.Substring(delimiterIndex + 1);
			}
			tokens[delimiters.Length] = dataLine;
		}

		/// <summary>
		/// 分解指定起始结束位置的若干单词
		/// </summary>
		/// <param name="startIndex">单词的起始位置</param>
		/// <param name="endIndex">单词的结束位置</param>
		public void Analyse(string dataLine, int[] startIndex,int[] endIndex)
		{
			tokens = new string[startIndex.Length];
			int length = dataLine.Length;
			for (int i = 0; i < startIndex.Length; i++)
			{
				if (endIndex[i] <= length )
				{
					tokens[i] = dataLine.Substring(startIndex[i], endIndex[i] - startIndex[i]);
					tokens[i] = tokens[i].Trim();
				}                
			}
		}
		/// <summary>
		/// 分解指定起始结束位置的单个词
		/// </summary>
		/// <param name="dataLine">需要解析的字符串</param>
		/// <param name="startIndex">开始位置</param>
		/// <param name="endIndex">结束位置</param>
		/// <returns>提取结果</returns>
		public string Analyse(string dataLine, int startIndex,int endIndex)
		{
			string result = "";
			int length = dataLine.Length;
			if (endIndex <= length )
			{
				result = dataLine.Substring(startIndex, endIndex - startIndex);
			}
			return result;
		}

		/// <summary>
		/// 获取某个索引位置的单词
		/// </summary>
		/// <param name="index">索引位置，从0开始</param>
		/// <returns>索引位置对应的单词</returns>
		public string GetElementAt(int index)
		{
			if (index < tokens.Length)
			{
				return tokens[index];
			}
			else
			{
				return null;
			}
		}
        
		/// <summary>
		/// 获取解析出来的单词数量
		/// </summary>
		public int Length
		{
			get 
			{
				if (tokens == null)
				{
					return 0;
				}
				else
				{
					return tokens.Length;
				}                 
			}
		}

	}
}
