import { test, expect } from '@playwright/test';

/**
 * Test de diagnostic pour vérifier la connexion au backend
 */
test.describe('Test de Connexion Backend', () => {
  test('devrait pouvoir se connecter au backend en HTTPS', async ({ page }) => {
    // Aller sur la page d'inscription
    await page.goto('/auth/register');
    await page.waitForSelector('form', { timeout: 10000 });
    
    // Test 1: Vérifier que le backend répond via page.request
    console.log('🔍 Test 1: Connexion via page.request...');
    try {
      const response = await page.request.get('https://localhost:7001/swagger', {
        ignoreHTTPSErrors: true,
        timeout: 5000
      });
      console.log(`✅ Backend accessible via page.request: ${response.status()}`);
    } catch (error: any) {
      console.error(`❌ Erreur page.request: ${error.message}`);
    }
    
    // Test 2: Vérifier que fetch fonctionne depuis le navigateur
    console.log('🔍 Test 2: Connexion via fetch depuis le navigateur...');
    const fetchTest = await page.evaluate(async () => {
      try {
        const response = await fetch('https://localhost:7001/swagger', {
          method: 'GET',
          mode: 'cors'
        });
        return {
          success: true,
          status: response.status,
          ok: response.ok
        };
      } catch (error: any) {
        return {
          success: false,
          error: error.message,
          name: error.name
        };
      }
    });
    console.log('📊 Résultat fetch:', JSON.stringify(fetchTest, null, 2));
    
    // Test 3: Tester OPTIONS (CORS preflight)
    console.log('🔍 Test 3: Test CORS preflight (OPTIONS)...');
    const corsTest = await page.evaluate(async () => {
      try {
        const response = await fetch('https://localhost:7001/api/auth/register', {
          method: 'OPTIONS',
          headers: {
            'Origin': 'http://localhost:4200',
            'Access-Control-Request-Method': 'POST',
            'Access-Control-Request-Headers': 'content-type'
          }
        });
        return {
          success: true,
          status: response.status,
          headers: Object.fromEntries(response.headers.entries())
        };
      } catch (error: any) {
        return {
          success: false,
          error: error.message,
          name: error.name
        };
      }
    });
    console.log('📊 Résultat CORS:', JSON.stringify(corsTest, null, 2));
    
    // Test 4: Tester POST avec un payload simple
    console.log('🔍 Test 4: Test POST avec payload...');
    const postTest = await page.evaluate(async () => {
      try {
        const response = await fetch('https://localhost:7001/api/auth/register', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Origin': 'http://localhost:4200'
          },
          body: JSON.stringify({
            email: 'test@example.com',
            password: 'Test1234!@#$',
            confirmPassword: 'Test1234!@#$',
            firstName: 'Test',
            lastName: 'User'
          })
        });
        const text = await response.text();
        return {
          success: true,
          status: response.status,
          body: text.substring(0, 500)
        };
      } catch (error: any) {
        return {
          success: false,
          error: error.message,
          name: error.name,
          stack: error.stack?.substring(0, 500)
        };
      }
    });
    console.log('📊 Résultat POST:', JSON.stringify(postTest, null, 2));
    
    // Vérifier que au moins le test 1 passe
    expect(fetchTest.success || corsTest.success).toBeTruthy();
  });
});
