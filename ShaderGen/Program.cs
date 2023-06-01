using PowerOfMind.Graphics.Shader;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

string workingDir = null;
string outputDir = null;

var buildFiles = new Dictionary<string, string>();
var ignoreFiles = new HashSet<string>();

bool buildAll = false;

for(int i = 0; i < args.Length; i++)
{
	switch(args[i])
	{
		case "-w"://work dir
			if(i + 1 < args.Length)
			{
				i++;
				workingDir = args[i];
			}
			break;
		case "-o"://output dir
			if(i + 1 < args.Length)
			{
				i++;
				outputDir = args[i];
			}
			break;
		case "-f"://input file
			if(i + 1 < args.Length)
			{
				i++;
				buildFiles.Add(args[i].Replace('\\', '/').Trim('/').ToLowerInvariant(), args[i]);
			}
			break;
		case "-i"://ignore file
			if(i + 1 < args.Length)
			{
				i++;
				ignoreFiles.Add(args[i].Replace('\\', '/').Trim('/').ToLowerInvariant());
			}
			break;
		case "-a"://build all files in folder
			buildAll = true;
			break;
	}
}

bool notFoundAny = false;
if(string.IsNullOrEmpty(workingDir))
{
	Console.WriteLine("Working directory not specified");
	notFoundAny = true;
}
else if(!Directory.Exists(workingDir))
{
	Console.WriteLine("Working directory does not exist");
	notFoundAny = true;
}

if(string.IsNullOrEmpty(outputDir))
{
	Console.WriteLine("Output directory not specified");
	notFoundAny = true;
}
else if(!Directory.Exists(outputDir))
{
	Console.WriteLine("Output directory does not exist");
	notFoundAny = true;
}

if(notFoundAny) return;

workingDir = Path.GetFullPath(workingDir);
outputDir = Path.GetFullPath(outputDir);

buildFiles = buildFiles.Where(pair => {
	if(!File.Exists(Path.Combine(workingDir, pair.Value)))
	{
		Console.WriteLine(string.Format("File {0} does not exist", pair.Value));
		return false;
	}
	return true;
}).ToDictionary(p => p.Key, p => p.Value);

if(buildAll)
{
	foreach(var file in Directory.EnumerateFiles(workingDir, "*.*", SearchOption.AllDirectories))
	{
		switch(Path.GetExtension(file))
		{
			case ".vsh":
			case ".fsh":
			case ".gsh":
			case ".ash":
				var path = Path.GetRelativePath(workingDir, file);
				buildFiles.Add(path.Replace('\\', '/').Trim('/').ToLowerInvariant(), path);
				break;
		}
	}
}
foreach(var file in ignoreFiles)
{
	buildFiles.Remove(file);
}

var jsonOptions = new JsonSerializerOptions() {
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	IncludeFields = true,
	IgnoreReadOnlyFields = true,
	IgnoreReadOnlyProperties = true,
	DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
	PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	WriteIndented = true
};

var sourceList = new List<string>();
var source2index = new Dictionary<string, int>();
foreach(var pair in buildFiles)
{
	try
	{
		var code = File.ReadAllText(Path.Combine(workingDir, pair.Value));
		var shader = ShaderParser.ParseShader((id) => {
			if(id == 0) return code;
			return sourceList[id];
		}, name => {
			name = name.Replace('\\', '/').Trim('/').ToLowerInvariant();
			if(!source2index.TryGetValue(name, out var index))
			{
				index = sourceList.Count;
				sourceList.Add(name);
				source2index[name] = index;
			}
			return index;
		});
		var path = Path.Combine(outputDir, pair.Value);
		if(!Directory.Exists(Path.GetDirectoryName(path)))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
		}
		path = path.Substring(0, path.Length - 1) + "c";//vsh to vsc
		File.WriteAllText(path, JsonSerializer.Serialize(new ShaderCodeFile(shader, sourceList), jsonOptions));
	}
	catch(Exception e)
	{
		Console.WriteLine("Exception white trying to build shader '{0}':\n{1}", pair.Value, e);
	}
}

class ShaderCodeFile
{
	public int? Version, VersionPos;
	public string Code;
	public FieldInfo[] Inputs;
	public FieldInfo[] Uniforms;
	public (string, int)[] Includes;

	public ShaderCodeFile(ShaderParser.ShaderCode shader, List<string> sourceList)
	{
		this.Version = shader.Version < 0 ? null : shader.Version;
		this.VersionPos = shader.VersionNumberPos < 0 ? null : shader.VersionNumberPos;
		this.Code = shader.Code;
		this.Uniforms = Array.ConvertAll(shader.Uniforms, f => new FieldInfo(f));
		this.Inputs = Array.ConvertAll(shader.Inputs, f => new FieldInfo(f));
		this.Includes = Array.ConvertAll(shader.IncludeAtPos, pair => (sourceList[pair.Key], pair.Value));
	}

	public class FieldInfo
	{
		static readonly Dictionary<Regex, FieldTypeProcessor> processors = new Dictionary<Regex, FieldTypeProcessor>() {
			{ new Regex("^bvec(\\d+)$"), ProcessVecB },
			{ new Regex("^ivec(\\d+)$"), ProcessVecI },
			{ new Regex("^uvec(\\d+)$"), ProcessVecU },
			{ new Regex("^vec(\\d+)$"), ProcessVecF },
			{ new Regex("^mat(\\d+)$"), ProcessMat },
			{ new Regex("^mat(\\d+)x(\\d+)$"), ProcessMatX }
		};

		public int? Location;
		public string Name;
		public string Alias;
		public string Type;
		public int Size;

		public FieldInfo(ShaderParser.FieldInfo info)
		{
			this.Location = info.Location < 0 ? null : info.Location;
			this.Name = info.Name;
			this.Alias = info.Alias;
			this.Type = info.TypeName;
			this.Size = 1;

			foreach(var pair in processors)
			{
				var match = pair.Key.Match(info.TypeName);
				if(match.Success)
				{
					pair.Value(match, out this.Type, out this.Size);
					break;
				}
			}
		}

		delegate void FieldTypeProcessor(Match match, out string type, out int size);

		private static void ProcessVecB(Match match, out string type, out int size)
		{
			type = "bool";
			size = int.Parse(match.Groups[1].Value);
		}

		private static void ProcessVecI(Match match, out string type, out int size)
		{
			type = "int";
			size = int.Parse(match.Groups[1].Value);
		}

		private static void ProcessVecU(Match match, out string type, out int size)
		{
			type = "uint";
			size = int.Parse(match.Groups[1].Value);
		}

		private static void ProcessVecF(Match match, out string type, out int size)
		{
			type = "float";
			size = int.Parse(match.Groups[1].Value);
		}

		private static void ProcessMat(Match match, out string type, out int size)
		{
			type = "float";
			int c = int.Parse(match.Groups[1].Value);
			size = c * c;
		}

		private static void ProcessMatX(Match match, out string type, out int size)
		{
			type = "float";
			size = int.Parse(match.Groups[1].Value) * int.Parse(match.Groups[2].Value);
		}
	}
}