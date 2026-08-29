/**
 * Playwright global setup for CI: verify the backend is reachable before smoke tests run.
 * User registration is handled by scripts/ci/e2e-register-user.sh in the GitHub Actions workflow.
 */
async function waitForBackend(baseUrl: string, maxAttempts = 60, sleepMs = 2000): Promise<void> {
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(`${baseUrl}/swagger`, { method: 'GET' });
      if (response.status < 500) {
        console.log(`[ci-global-setup] Backend ready at ${baseUrl} (attempt ${attempt}).`);
        return;
      }
    } catch {
      // retry
    }
    console.log(`[ci-global-setup] Waiting for backend (${attempt}/${maxAttempts})...`);
    await new Promise(resolve => setTimeout(resolve, sleepMs));
  }
  throw new Error(`Backend not ready at ${baseUrl} after ${maxAttempts} attempts.`);
}

export default async function globalSetup(): Promise<void> {
  if (!process.env.CI) {
    return;
  }

  const baseUrl = process.env.E2E_API_BASE ?? 'https://localhost:7001';
  await waitForBackend(baseUrl);
}
