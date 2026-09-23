type NavigationItem = {
  path?: string;
  text: string;
  icon?: string;
  items?: NavigationItem[];
  roles?: string[];
};
export const navigation: NavigationItem[] = [
  {
    text: 'Home',
    path: '/home',
    icon: 'home',
  },
  {
    text: 'Companies',
    path: '/companies',
    icon: 'folder',
    roles: ['Admin', 'User']
  },
  {
    text: 'Customers',
    path: '/customers',
    icon: 'folder',
    roles: ['Admin', 'User']
  },
  {
    text: 'Users',
    path: '/users',
    icon: 'folder',
    roles: ['Admin']
  },
  // {
  //   text: 'Examples',
  //   icon: 'folder',
  //   items: [
  //     {
  //       text: 'Profile',
  //       path: '/profile'
  //     },
  //     {
  //       text: 'Tasks',
  //       path: '/tasks'
  //     }
  //   ]
  // }
];
