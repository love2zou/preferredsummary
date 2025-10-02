import { authService } from './authService';
import TestAccountConfig from '@/config/testAccount';
import { ElMessage } from 'element-plus';

class AutoLoginService {
  private static readonly TOKEN_KEY = 'guest_token';
  private static readonly TOKEN_EXPIRY_KEY = 'guest_token_expiry';
  private static readonly TOKEN_DURATION = 24 * 60 * 60 * 1000; // 24小时

  // 检查是否有有效的访客token
  static hasValidGuestToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    const expiry = localStorage.getItem(this.TOKEN_EXPIRY_KEY);
    
    if (!token || !expiry) {
      return false;
    }
    
    return Date.now() < parseInt(expiry);
  }

  // 移除了 getGuestToken 方法

  // 自动登录获取token
  static async autoLogin(): Promise<string | null> {
    try {
      // 检查测试账号配置
      if (!TestAccountConfig.isTestAccountValid()) {
        console.warn('测试账号配置无效');
        return null;
      }

      // 获取测试账号信息
      const testAccount = TestAccountConfig.getTestAccount();
      
      // 使用测试账号登录
      const response = await authService.login({
        username: testAccount.username,
        password: testAccount.password
      });

      if (response.success && response.data?.token) {
        // 存储token和过期时间（统一使用 token 和 tokenExpireTime）
        const expiryTime = Date.now() + this.TOKEN_DURATION;
        localStorage.setItem('token', response.data.token);
        localStorage.setItem('tokenExpireTime', expiryTime.toString());
        
        console.log('自动登录成功，获取token');
        return response.data.token;
      }
      
      return null;
    } catch (error) {
      console.error('自动登录失败:', error);
      throw error; // 抛出错误以便上层处理
    }
  }

  // 清除访客token
  static clearGuestToken(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRY_KEY);
  }

  // 检查是否为访客模式
  static isGuestMode(): boolean {
    const userToken = localStorage.getItem('token');
    const guestToken = this.hasValidGuestToken() ? localStorage.getItem(this.TOKEN_KEY) : null;
    return !userToken && !!guestToken;
  }

  // 检查是否已登录
  static isValidLogin(): boolean {
    const token = localStorage.getItem('token')
    const expireTime = localStorage.getItem('tokenExpireTime')
    
    if (!token || !expireTime) {
      return false
    }
    
    const now = new Date().getTime()
    const expire = parseInt(expireTime)
    
    return now < expire
  }
  
  // 获取当前token
  static getCurrentToken(): string | null {
    if (this.isValidLogin()) {
      return localStorage.getItem('token')
    }
    return null
  }
  
  // 确保已登录，如果未登录则自动登录
  static async ensureLoggedIn(): Promise<void> {
    if (!this.isValidLogin()) {
      await this.autoLogin()
    }
  }
}

export default AutoLoginService;