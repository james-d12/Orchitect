import { atom } from "nanostores";

interface AuthState {
  accessToken: string | null;
  isAuthenticated: boolean;
}

// Initialize from localStorage if available
const getInitialState = (): AuthState => {
  if (typeof window !== "undefined") {
    const token = localStorage.getItem("accessToken");
    return {
      accessToken: token,
      isAuthenticated: !!token,
    };
  }
  return {
    accessToken: null,
    isAuthenticated: false,
  };
};

export const authStore = atom<AuthState>(getInitialState());

// Actions
export const setAccessToken = (token: string) => {
  if (typeof window !== "undefined") {
    localStorage.setItem("accessToken", token);
  }
  authStore.set({
    accessToken: token,
    isAuthenticated: true,
  });
};

export const clearAccessToken = () => {
  if (typeof window !== "undefined") {
    localStorage.removeItem("accessToken");
  }
  authStore.set({
    accessToken: null,
    isAuthenticated: false,
  });
};

export const getAccessToken = (): string | null => {
  return authStore.get().accessToken;
};

export const isAuthenticated = (): boolean => {
  return authStore.get().isAuthenticated;
};
