namespace SwMateAI.Core.Skills
{
    public class SkillResult
    {
        public bool IsSuccess { get; private set; }
        public object Data { get; private set; }
        public string Error { get; private set; } = string.Empty;

        public static SkillResult Success(object data = null) => new SkillResult { IsSuccess = true, Data = data };
        public static SkillResult Failure(string error) => new SkillResult { IsSuccess = false, Error = error ?? string.Empty };
    }
}
