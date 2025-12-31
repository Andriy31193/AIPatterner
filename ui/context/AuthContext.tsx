// Authentication context provider for managing user session
'use client';

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { apiService } from '@/services/api';
import type { User, LoginRequest } from '@/types';

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (data: { username: string; email: string; password: string }) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Helper function to save user to localStorage
  const saveUserToStorage = (userData: User | null) => {
    if (typeof window !== 'undefined') {
      if (userData) {
        localStorage.setItem('auth_user', JSON.stringify(userData));
      } else {
        localStorage.removeItem('auth_user');
      }
    }
  };

  // Helper function to load user from localStorage
  const loadUserFromStorage = (): User | null => {
    if (typeof window === 'undefined') return null;
    const userData = localStorage.getItem('auth_user');
    if (userData) {
      try {
        return JSON.parse(userData) as User;
      } catch (e) {
        console.error('Failed to parse user data from localStorage', e);
        return null;
      }
    }
    return null;
  };

  useEffect(() => {
    // Check for existing token and restore user from localStorage
    const token = typeof window !== 'undefined' ? localStorage.getItem('auth_token') : null;
    if (token) {
      // Restore user from localStorage if token exists
      const savedUser = loadUserFromStorage();
      if (savedUser) {
        setUser(savedUser);
      } else {
        // Token exists but no user data - clear invalid token
        apiService.logout();
      }
      // TODO: Validate token with backend
      // In production, you'd decode and validate the JWT
    } else {
      // No token means not authenticated, clear any stale user data
      saveUserToStorage(null);
    }
    setIsLoading(false);
  }, []);

  const login = async (credentials: LoginRequest) => {
    const response = await apiService.login(credentials);
    setUser(response.user);
    saveUserToStorage(response.user);
  };

  const register = async (data: { username: string; email: string; password: string }) => {
    const response = await apiService.register(data);
    setUser(response.user);
    saveUserToStorage(response.user);
  };

  const logout = () => {
    apiService.logout();
    setUser(null);
    saveUserToStorage(null);
  };

  const value: AuthContextType = {
    user,
    isLoading,
    isAuthenticated: !!user,
    isAdmin: user?.role === 'admin',
    login,
    register,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

