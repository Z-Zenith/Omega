import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { resetUserPassword } from './api'

// Regression test for #149: the backend's ResetPasswordRequest requires a JSON
// object body ({"newPassword": "..."}), not a bare JSON string.
describe('resetUserPassword (#149)', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    // jsdom's localStorage isn't wired up in this environment's vitest config
    // (unrelated to #149) - stub the minimal surface getToken()/request() need.
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => store.set(k, v),
      removeItem: (k: string) => store.delete(k),
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the new password as a {newPassword} object, not a bare string', async () => {
    await resetUserPassword('user-1', 'aB3$xyzsecure')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [, options] = fetchMock.mock.calls[0]
    expect(JSON.parse(options.body as string)).toEqual({ newPassword: 'aB3$xyzsecure' })
  })
})
