// ---------------------------------------------------------------------
    // Helper: Get or create a user record for an author
    // Email-first approach: Only call GitHub API for unknown emails
    // ---------------------------------------------------------------------
    private async Task<User> GetOrCreateAuthorUser(
        string email,
        string username,
        string repoOwner,
        string repoName,
        string commitSha,
        Guid repositoryId)
    {
        // Step 1: Parse noreply emails to extract GitHub username and ID
        string? githubUsername = null;
        long githubId = 0;
        
        if (!string.IsNullOrWhiteSpace(email) && email.Contains("@users.noreply.github.com"))
        {
            var parts = email.Split('@')[0].Split('+');
            if (parts.Length == 2)
            {
                githubUsername = parts[1]; // Real GitHub username
                long.TryParse(parts[0], out githubId);
                _logger.LogInformation($"📧 Parsed noreply email: '{email}' → GitHub username: '{githubUsername}', ID: {githubId}");
            }
        }

        // Step 2: For REAL emails (not noreply), check if user exists by email FIRST
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com"))
        {
            var existingByEmail = await _db.GetUserByEmail(email);
            if (existingByEmail != null)
            {
                _logger.LogInformation($"✅ Found existing user by email '{email}': {existingByEmail.AuthorName}");
                return existingByEmail;
            }
            
            // Email not found - need to call GitHub API to get real username
            _logger.LogInformation($"🔍 Unknown email '{email}' - calling GitHub API for commit {commitSha[..7]}");
            
            try
            {
                var commitAuthor = await _github.GetCommitAuthor(repoOwner, repoName, commitSha);
                if (commitAuthor != null)
                {
                    githubUsername = commitAuthor.Login;
                    githubId = commitAuthor.Id;
                    _logger.LogInformation($"✅ GitHub API resolved: {githubUsername} (ID: {githubId})");
                }
                else
                {
                    _logger.LogWarning($"⚠️ GitHub API returned null for commit {commitSha[..7]}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠️ GitHub API call failed for commit {commitSha[..7]}: {ex.Message}");
            }
        }

        // Step 3: If we have GitHub username, check if user exists
        if (!string.IsNullOrWhiteSpace(githubUsername))
        {
            // First try by GitHub ID
            if (githubId > 0)
            {
                var existingById = await _db.GetUserByGitHubId(githubId);
                if (existingById != null)
                {
                    // Update email if different
                    if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com") && existingById.Email != email)
                    {
                        await _db.UpdateUserEmail(existingById.Id, email);
                        existingById.Email = email;
                        _logger.LogInformation($"📝 Updated email for user '{existingById.AuthorName}' to '{email}'");
                    }
                    return existingById;
                }
            }
            
            // Then try by GitHub username
            var existingByUsername = await _db.GetUserByAuthorName(githubUsername);
            if (existingByUsername != null)
            {
                // Update email if different
                if (!string.IsNullOrWhiteSpace(email) && !email.Contains("@users.noreply.github.com") && existingByUsername.Email != email)
                {
                    await _db.UpdateUserEmail(existingByUsername.Id, email);
                    existingByUsername.Email = email;
                    _logger.LogInformation($"📝 Updated email for user '{existingByUsername.AuthorName}' to '{email}'");
                }
                return existingByUsername;
            }
            
            // User doesn't exist - ALWAYS call GitHub API to get real username before creating
            if (string.IsNullOrWhiteSpace(email) || email.Contains("@users.noreply.github.com"))
            {
                _logger.LogInformation($"🔍 Calling GitHub API to get real username for commit {commitSha[..7]}");
                try
                {
                    var commitAuthor = await _github.GetCommitAuthor(repoOwner, repoName, commitSha);
                    if (commitAuthor != null)
                    {
                        // Update githubId and username if we got better data
                        githubId = commitAuthor.Id;
                        githubUsername = commitAuthor.Login;
                        _logger.LogInformation($"✅ GitHub API confirmed: {githubUsername} (ID: {githubId})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ GitHub API call failed: {ex.Message}");
                }
            }
            
            // Create user - set email to null for noreply, or use real email
            var userEmail = email != null && email.Contains("@users.noreply.github.com") ? null : email;
            
            var newUser = await _db.CreateUser(new User
            {
                GithubId = githubId,
                AuthorName = githubUsername, // Use GitHub username, NOT Git config name
                Email = userEmail,
                AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(githubUsername)}"
            });
            
            _logger.LogInformation($"➕ Created new user: {githubUsername} (GitHub ID: {githubId}, Email: {userEmail ?? "none"})");
            return newUser;
        }

        // Step 4: Fallback - couldn't get GitHub username (API failed or noreply parse failed)
        // Use Git config name as last resort
        _logger.LogWarning($"⚠️ Could not resolve GitHub username for '{username}' - using Git config name as fallback");
        
        var fallbackUser = await _db.GetUserByAuthorName(username);
        if (fallbackUser != null)
        {
            return fallbackUser;
        }
        
        var fallbackEmail = email != null && email.Contains("@users.noreply.github.com") ? null : email;
        var createdFallbackUser = await _db.CreateUser(new User
        {
            GithubId = 0,
            AuthorName = username, // Fallback to Git config name
            Email = fallbackEmail,
            AvatarUrl = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(username)}"
        });
        
        _logger.LogWarning($"➕ Created fallback user: {username}");
        return createdFallbackUser;
    }