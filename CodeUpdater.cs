using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectToPromptScanner
{
    public class ChangeAnalysis
    {
        public string UpdatedCode { get; set; }
        public int TotalBlocks { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> ErrorDetails { get; set; } = new List<string>();
    }

    class CodeUpdater
    {
        enum ParsingState
        {
            OutsideChange,
            InsideOldCode,
            InsideNewCode
        }

        public static ChangeAnalysis ApplyAiChanges(string originalCode, string aiResponse, string filePath = "")
        {
            var analysis = new ChangeAnalysis();
            bool isCSharp = !string.IsNullOrEmpty(filePath) && filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            bool useCrlf = isCSharp && originalCode.Contains("\r\n");

            string normalizedAiResponse = aiResponse.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] aiLines = normalizedAiResponse.Split('\n');

            ParsingState currentState = ParsingState.OutsideChange;

            List<string> oldBlock = new List<string>();
            List<string> newBlock = new List<string>();
            string currentBlockId = "";

            string updatedCode = originalCode.Replace("\r\n", "\n").Replace("\r", "\n");

            foreach (var line in aiLines) {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("//[START_CHANGE_")) {
                    if (currentState != ParsingState.OutsideChange) continue;

                    currentBlockId = trimmedLine.Replace("//[", "").Replace("]", "");
                    currentState = ParsingState.InsideOldCode;
                    oldBlock.Clear();
                    newBlock.Clear();
                    continue;
                }

                if (trimmedLine == "//[OLD_CODE]") {
                    currentState = ParsingState.InsideOldCode;
                    continue;
                }

                if (trimmedLine == "//[NEW_CODE]") {
                    currentState = ParsingState.InsideNewCode;
                    continue;
                }

                if (trimmedLine.StartsWith("//[END_CHANGE_")) {
                    string endBlockId = trimmedLine.Replace("//[", "").Replace("]", "").Replace("END_", "START_");

                    if (currentState != ParsingState.OutsideChange && currentBlockId == endBlockId) {
                        analysis.TotalBlocks++;
                        updatedCode = PerformFlexibleReplacement(updatedCode, oldBlock, newBlock, currentBlockId, isCSharp, out bool isSuccess, out string errorMsg);

                        if (isSuccess) {
                            analysis.SuccessCount++;
                        }
                        else {
                            analysis.FailCount++;
                            analysis.ErrorDetails.Add(errorMsg);
                        }
                    }

                    currentState = ParsingState.OutsideChange;
                    continue;
                }

                if (currentState == ParsingState.InsideOldCode) {
                    oldBlock.Add(line);
                }
                else if (currentState == ParsingState.InsideNewCode) {
                    newBlock.Add(line);
                }
            }

            // Bảo toàn chuẩn xuống dòng CRLF cho file C# nếu file gốc dùng CRLF
            if (useCrlf) {
                analysis.UpdatedCode = updatedCode.Replace("\n", "\r\n");
            }
            else {
                analysis.UpdatedCode = updatedCode;
            }

            return analysis;
        }

        private static string PerformFlexibleReplacement(string currentCode, List<string> oldBlock, List<string> newBlock, string blockId, bool isCSharp, out bool isSuccess, out string errorMessage)
        {
            string oldText = string.Join("\n", oldBlock).Trim();
            string newText = string.Join("\n", newBlock).Trim();

            isSuccess = false;
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(oldText)) {
                isSuccess = true;
                return currentCode + "\n\n" + newText;
            }

            if (isCSharp) {
                // 1. Kiểm tra khớp chính xác chuỗi trước
                if (currentCode.Contains(oldText)) {
                    currentCode = currentCode.Replace(oldText, newText);
                    isSuccess = true;
                    return currentCode;
                }

                // 2. Chuẩn hóa dấu ngoặc nhọn ({, }) để xử lý khác biệt giữa Allman và K&R style
                string paddedOld = Regex.Replace(oldText, @"([{}])", " $1 ");
                string[] tokens = Regex.Split(paddedOld, @"\s+").Where(t => !string.IsNullOrEmpty(t)).ToArray();

                var patternBuilder = new StringBuilder();
                for (int i = 0; i < tokens.Length; i++) {
                    if (i > 0) {
                        string prev = tokens[i - 1];
                        string curr = tokens[i];
                        // Cho phép linh hoạt khoảng trắng/xuống dòng xung quanh dấu ngoặc nhọn
                        if (prev == "{" || prev == "}" || curr == "{" || curr == "}") {
                            patternBuilder.Append(@"\s*");
                        }
                        else {
                            patternBuilder.Append(@"\s+");
                        }
                    }
                    patternBuilder.Append(Regex.Escape(tokens[i]));
                }

                string pattern = patternBuilder.ToString();

                try {
                    if (Regex.IsMatch(currentCode, pattern)) {
                        Regex regex = new Regex(pattern);
                        string safeReplacement = newText.Replace("$", "$$");
                        currentCode = regex.Replace(currentCode, safeReplacement, 1);
                        isSuccess = true;
                        return currentCode;
                    }
                }
                catch (Exception ex) {
                    errorMessage = $"Lỗi Regex tại {blockId}: {ex.Message}";
                }

                // 3. Dự phòng cho C#: So khớp vị trí bỏ qua toàn bộ khoảng trắng (Fuzzy character match)
                if (!isSuccess && TryReplaceIgnoringWhitespace(ref currentCode, oldText, newText)) {
                    isSuccess = true;
                    return currentCode;
                }

                if (!isSuccess && string.IsNullOrEmpty(errorMessage)) {
                    errorMessage = $"Không tìm thấy đoạn code cũ của {blockId} trong file.";
                }

                return currentCode;
            }
            else {
                // Giữ nguyên logic cũ cho các định dạng file khác
                string[] tokens = Regex.Split(oldText, @"\s+");
                var escapedTokens = tokens.Where(t => !string.IsNullOrEmpty(t)).Select(Regex.Escape);
                string pattern = string.Join(@"\s+", escapedTokens);

                try {
                    if (Regex.IsMatch(currentCode, pattern)) {
                        Regex regex = new Regex(pattern);
                        string safeReplacement = newText.Replace("$", "$$");
                        currentCode = regex.Replace(currentCode, safeReplacement, 1);
                        isSuccess = true;
                    }
                    else {
                        if (currentCode.Contains(oldText)) {
                            currentCode = currentCode.Replace(oldText, newText);
                            isSuccess = true;
                        }
                        else {
                            errorMessage = $"Không tìm thấy đoạn code cũ của {blockId} trong file.";
                        }
                    }
                }
                catch (Exception ex) {
                    errorMessage = $"Lỗi Regex tại {blockId}: {ex.Message}";
                }

                return currentCode;
            }
        }

        private static bool TryReplaceIgnoringWhitespace(ref string currentCode, string oldText, string newText)
        {
            var cleanCurrentBuilder = new StringBuilder();
            var charMap = new List<int>();

            for (int i = 0; i < currentCode.Length; i++) {
                if (!char.IsWhiteSpace(currentCode[i])) {
                    cleanCurrentBuilder.Append(currentCode[i]);
                    charMap.Add(i);
                }
            }

            string cleanCurrent = cleanCurrentBuilder.ToString();
            string cleanOld = new string(oldText.Where(c => !char.IsWhiteSpace(c)).ToArray());

            if (string.IsNullOrEmpty(cleanOld)) return false;

            int matchIndex = cleanCurrent.IndexOf(cleanOld, StringComparison.Ordinal);
            if (matchIndex >= 0) {
                int startIndex = charMap[matchIndex];
                int endIndex = charMap[matchIndex + cleanOld.Length - 1] + 1;

                currentCode = currentCode.Substring(0, startIndex) + newText + currentCode.Substring(endIndex);
                return true;
            }

            return false;
        }
    }
}