import CryptoUtil from '@/utils/crypto';

// 测试账号配置（加密存储）
class TestAccountConfig {
  // 加密后的测试账号信息
  private static readonly ENCRYPTED_USERNAME = 'dGVzdF91c2VycHJlZmVycmVkX2FwcF8yMDI0';
  private static readonly ENCRYPTED_PASSWORD = 'QFpxMTIzNDU2Nzg5cHJlZmVycmVkX2FwcF8yMDI0';

  // 获取解密后的测试账号
  static getTestAccount() {
    return {
      username: CryptoUtil.decrypt(this.ENCRYPTED_USERNAME),
      password: CryptoUtil.decrypt(this.ENCRYPTED_PASSWORD)
    };
  }

  // 验证测试账号是否可用
  static isTestAccountValid(): boolean {
    const account = this.getTestAccount();
    return account.username.length > 0 && account.password.length > 0;
  }
}

export default TestAccountConfig;