import axios from 'axios';

const STATIC_BASE_URL = 'http://localhost:5182';
const API_BASE_URL = `${STATIC_BASE_URL}/api`;

const api = axios.create({
  baseURL: API_BASE_URL,
});

// Thêm interceptor để đính kèm JWT token vào các request
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
}, (error) => {
  return Promise.reject(error);
});

export default api;
export { API_BASE_URL, STATIC_BASE_URL };
