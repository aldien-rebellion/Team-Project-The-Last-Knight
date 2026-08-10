---
description: Scrutinize code for bugs, security vulnerabilities, and logic errors.
globs: "*.*"
---

# Scrutinize AI Skill

When the user calls `/scrutinize`, analyze the code/file in context thoroughly:

1. **Security Vulnerabilities**: Identify OWASP risks, memory leaks, or unhandled errors.
2. **Logic & Edge Cases**: Check for race conditions, null pointer risks, or improper state management.
3. **Performance & Clean Code**: Suggest specific code fixes and optimizations.

### Output Format:
- **Severity Summary** (High / Medium / Low)
- **Detailed Issues & Root Cause**
- **Recommended Refactored Code**