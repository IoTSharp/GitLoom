using LibGit2Sharp;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
const string AssemblyVersionElement = "AssemblyVersion";
const string FileVersionElement = "FileVersion";
const string InformationalVersionElement = "InformationalVersion";
const string VersionElement = "Version";
var rep_dir = LibGit2Sharp.Repository.Discover(Directory.GetCurrentDirectory());
if (rep_dir == null)
{
    Console.WriteLine("Not found git Repository");
}
else
{
    var rep = new Repository(rep_dir);
    var tag = rep.Tags.OrderDescending().FirstOrDefault();
    var gv = new GitVerInfo() { VersionMode = VersionMode.MajorMinorCommits };
    gv.Commits = rep.Commits.Count();
    var tvary = tag.FriendlyName?.TrimStart('V', 'v')?.Split('.');
    if (tvary?.Length >= 2)
    {
        gv.Major = int.Parse(tvary[0]);
        gv.Minor = int.Parse(tvary[1]);
        gv.EndTag = tvary?.Length >= 3 ? int.Parse(tvary[2] + ".0") : 0;
        var _cw = (tag.Target as Commit)?.Author.When;
        gv.Patch = rep.Commits.Where(c => c.Author.When >= _cw).Count();
    }
    gv.BranchName = rep.Head.FriendlyName;
    var commit = rep.Commits.First();
    gv.CommitDate = commit.Author.When.DateTime;
    gv.Sha = commit.Sha;
    var status = rep.RetrieveStatus();
    string info = string.Format(CultureInfo.InvariantCulture, "+{0} ~{1} -{2} | +{3} ~{4} -{5} | i{6}", status.Added.Count(), status.Staged.Count(), status.Removed.Count(), status.Untracked.Count(), status.Modified.Count(), status.Missing.Count(), status.Ignored.Count());
    gv.UncommittedChanges = status.Added.Count() + status.Staged.Count() + status.Removed.Count() + status.Untracked.Count() + status.Modified.Count() + status.Missing.Count();
    var csprojects = System.IO.Directory.GetFiles(rep.Info.WorkingDirectory, "*.csproj", SearchOption.AllDirectories);
    foreach (var pjt in csprojects)
    {
        var originalFileContents = System.IO.File.ReadAllText(pjt);
        var xmpj = XElement.Parse(originalFileContents);

        if (!CanUpdateProjectFile(xmpj))
        {
            Console.WriteLine($"Unable to update file: {pjt}");

        }
        else
        {

            if (!string.IsNullOrWhiteSpace(gv.AssemblyVersion))
            {
                UpdateProjectVersionElement(xmpj, AssemblyVersionElement, gv.AssemblyVersion);
            }

            if (!string.IsNullOrWhiteSpace(gv.AssemblyFileVersion))
            {
                UpdateProjectVersionElement(xmpj, FileVersionElement, gv.AssemblyFileVersion);
            }

            if (!string.IsNullOrWhiteSpace(gv.InformationalVersion))
            {
                UpdateProjectVersionElement(xmpj, InformationalVersionElement, gv.InformationalVersion);
            }

            if (!string.IsNullOrWhiteSpace(gv.Version))
            {
                UpdateProjectVersionElement(xmpj, VersionElement, gv.Version);
            }

            var outputXmlString = xmpj.ToString();
            if (originalFileContents != outputXmlString)
            {
                System.IO.File.WriteAllText(pjt, outputXmlString);
            }
        }
    }
}





bool CanUpdateProjectFile(XElement xmlRoot)
{
    if (xmlRoot.Name != "Project")
    {
        Console.WriteLine("Invalid project file specified, root element must be <Project>.");
        return false;
    }

    var sdkAttribute = xmlRoot.Attribute("Sdk");
    if (sdkAttribute?.Value.StartsWith("Microsoft.NET.Sdk") != true)
    {
        Console.WriteLine($"Specified project file Sdk ({sdkAttribute?.Value}) is not supported, please ensure the project sdk starts with 'Microsoft.NET.Sdk'");
        return false;
    }

    var propertyGroups = xmlRoot.Descendants("PropertyGroup").ToList();
    if (propertyGroups.Count == 0)
    {
        Console.WriteLine("Unable to locate any <PropertyGroup> elements in specified project file. Are you sure it is in a correct format?");
        return false;
    }

    var lastGenerateAssemblyInfoElement = propertyGroups.SelectMany(s => s.Elements("GenerateAssemblyInfo")).LastOrDefault();
    if (lastGenerateAssemblyInfoElement == null || (bool)lastGenerateAssemblyInfoElement) return true;
    Console.WriteLine("Project file specifies <GenerateAssemblyInfo>false</GenerateAssemblyInfo>: versions set in this project file will not affect the output artifacts.");
    return false;
}

static void UpdateProjectVersionElement(XElement xmlRoot, string versionElement, string versionValue)
{
    var propertyGroups = xmlRoot.Descendants("PropertyGroup").ToList();

    var propertyGroupToModify = propertyGroups.LastOrDefault(l => l.Element(versionElement) != null)
                                ?? propertyGroups[0];

    var versionXmlElement = propertyGroupToModify.Elements(versionElement).LastOrDefault();
    if (versionXmlElement != null)
    {
        versionXmlElement.Value = versionValue;
    }
    else
    {
        propertyGroupToModify.SetElementValue(versionElement, versionValue);
    }
}
public enum VersionMode
{
    MinorMinorPatch,
    MajorMinorCommits,
    LastTagName
}

public class GitVerInfo
{
    public VersionMode VersionMode { get; set; } = VersionMode.MinorMinorPatch;

    public string AssemblyFileVersion => $"{Major}.{Minor}.{GetVersionByMode()}";

    private int GetVersionByMode()
    {
        int result = 0;
        switch (VersionMode)
        {
            case VersionMode.LastTagName:
                result = EndTag;
                break;
            case VersionMode.MajorMinorCommits:
                result = Commits;
                break;
            case VersionMode.MinorMinorPatch:
            default:
                result = Patch;
                break;
        }
        return result;
    }

    public string AssemblyVersion => $"{Major}.{Minor}.{Commits}";

    public string BranchName { get; set; }


    public DateTime CommitDate { get; set; }

    public int Commits { get; set; }

    public string InformationalVersion => $"{Version}+{BranchName}@{Sha}&{CommitDate}{(UncommittedChanges > 0 ? $"+{UncommittedChanges}" : "")}";

    public int Major { get; set; }


    public int Minor { get; set; }

    public string Version => $"{Major}.{Minor}.{Commits}";

    public string Sha { get; set; }

    public string ShortSha => Sha.Substring(0, 8);

    public int UncommittedChanges { get; set; }
    public int Patch { get; set; }
    public int EndTag { get; set; }
}
