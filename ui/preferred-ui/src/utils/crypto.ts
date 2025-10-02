// 简单的加密解密工具
class CryptoUtil {
  private static readonly SECRET_KEY = 'preferred_app_2024';

  // 简单的Base64编码加密
  static encrypt(text: string): string {
    try {
      const combined = text + this.SECRET_KEY;
      return btoa(combined);
    } catch (error) {
      console.error('加密失败:', error);
      return '';
    }
  }

  // 简单的Base64解码解密
  static decrypt(encryptedText: string): string {
    try {
      const decoded = atob(encryptedText);
      return decoded.replace(this.SECRET_KEY, '');
    } catch (error) {
      console.error('解密失败:', error);
      return '';
    }
  }
}

export default CryptoUtil;