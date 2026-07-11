import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { login, ApiError } from './api'

// Regression test for #158: raw JSON error blobs must not be surfaced to users - the
// backend's {"error": "...", "message": "human text"} shape should be parsed and only
// `.message` shown, falling back to raw text/status if the body isn't that shape.
describe('request() error handling (#158)', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
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

  it('surfaces the backend message instead of the raw JSON body', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ error: 'invalid_password', message: 'Incorrect password.' }), { status: 401 }),
    )

    await expect(login('101', 'wrong', '000000')).rejects.toMatchObject({
      message: 'Incorrect password.',
      status: 401,
    })
  })

  it('falls back to the raw body when the response is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response('Internal Server Error', { status: 500, statusText: 'Internal Server Error' }))

    await expect(login('101', 'wrong', '000000')).rejects.toMatchObject({
      message: 'Internal Server Error',
    })
  })

  it('falls back to status text when the body is empty', async () => {
    fetchMock.mockResolvedValue(new Response('', { status: 500, statusText: 'Server Error' }))

    await expect(login('101', 'wrong', '000000')).rejects.toBeInstanceOf(ApiError)
  })
})
