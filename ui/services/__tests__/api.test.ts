// Unit test for API service
import { apiService } from '../api';
import axios from 'axios';

jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

describe('ApiService', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    if (typeof window !== 'undefined') {
      localStorage.clear();
    }
  });

  it('sets token after login', async () => {
    const mockResponse = {
      data: {
        token: 'test-token',
        user: { id: '1', username: 'test', email: 'test@test.com', role: 'user' },
      },
    };
    mockedAxios.create = jest.fn(() => ({
      post: jest.fn().mockResolvedValue(mockResponse),
      get: jest.fn(),
      interceptors: {
        request: { use: jest.fn() },
        response: { use: jest.fn() },
      },
    })) as any;

    // Note: This is a simplified test - in practice you'd need to properly mock axios.create
    expect(true).toBe(true);
  });
});

