using System.Collections.Generic;

namespace PatataGames;

public class JobScheduler<T> where T : struct
{
	private HashSet<T> jobs = new HashSet<T>();
}