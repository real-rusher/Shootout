using System.Text.RegularExpressions;

public static class ValidateUserInput
{
	public static string isUsernameValid(string userName)
	{
		if (userName == null)
		{
			return "";
		}
		string replacement = " ";
		string text = new Regex("\\s+").Replace(userName, replacement);
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		if (text.Length < 3 || text.Length > 15)
		{
			return "";
		}
		return text;
	}

	public static bool isIPCodeValid(string input)
	{
		if (input == null)
		{
			return false;
		}
		for (int i = 0; i < input.Length; i++)
		{
			if (!char.IsDigit(input[i]))
			{
				return false;
			}
		}
		return true;
	}
}
